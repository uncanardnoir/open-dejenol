# Fidelity audit

Standing rule: nothing in this port may be invented. Every rule traces to one of

1. the game's data files (`game_files\MDATA*.MDR`, `MORDOR.EXE`, `MG11.DLL`),
2. the game's own help (`MORDOR.HLP` in the game folder, which the port reads at start - the help
   window and the `helpquotes`, `spellhelp` and `castgate` probes show it; docs/GAME_FILES.md),
3. the community's reverse-engineered formulas (`docs\dejenol-formulas\`), or
4. a screenshot of the real game.

Anything that does not is listed below until it is fixed or removed.


## Module names used in this file

The decompiler emitted the modules as `MODULE1.BAS` … `MODULE13.BAS`; the original's own names
survive in its error-handler strings, and `tools/NameMap/names.tsv` records the mapping the
renamer applies to produce `decompiled-named/`. Citations below use both, so:

| cited as | file in `decompiled/` |
|---|---|
| MAINMOD | MODULE2 |
| MAZEMOD | MODULE3 |
| MONSMOD | MODULE4 |
| MAINMOD2 | MODULE6 |
| MULTIMOD | MODULE7 |
| MUSIC | MODULE9 |
| HALL | MODULE11 |
| GRAPHICS | MODULE12 |

Two of those mappings are inference rather than a clean string match, and are marked `named` in
`names.tsv` rather than `definitive`. MODULE12 carries one GRAPHICS error string and two MAZEMOD
ones; MODULE11 carries two HALL and one MAINMOD. In both cases the odd string names a routine that
plainly belongs to the file it sits in - device contexts and the clipboard in one, the library file
in the other - while the module it points at is already accounted for by another file with many
more strings. The likeliest explanation is the commonest one: an error handler copied from the
module the routine was moved out of, and never re-worded.

Line numbers cited under a MAZEMOD-style name refer to `decompiled-named/`, which differs by a few
lines from `decompiled/` (the renamer adds header comments); a MODULE-style citation refers to
`decompiled/` directly.

## Sourced and believed correct

| Rule | Source |
|---|---|
| **Combat is real time, not turn-based.** A timer runs at one tick a second the whole time you are below ground; each tick every living character in turn carries out their standing order and then their companions attack, and after all of them the monsters go | **decompiled source**, `timCombat.Interval = mc01B0` = 1000 (MAZEMOD.BAS:739), handler FRMDUNM.BAS:616, round MULTIMOD.BAS:497. The help says it too: "Since it takes place in real time, a player must be quick at the keyboard" |
| A round is not one blow: each equipped weapon swings character.Swings + weapon.Swings times, and only 15 of the game's 70 weapons swing once - most swing two to four, and the Vorpal Blade seven | **decompiled source**, MODULE4.BAS:333-340 |
| Critical hits and backstabs are rolled independently and both can land on the same blow - which the game calls a "Severed" hit, with its own sound and wording | **decompiled source**, MODULE4.BAS:440 and 453 |
| Their chances share a shape but differ in four places: critical divides the ability term by 3, uses Log(10) for the weapon term, floors at 1 and caps at 50; backstab divides by 2, uses Log(100), floors at 2 and caps at 75 | **decompiled source**, MODULE4.BAS:381-420 |
| The weapon term only applies when the weapon itself carries the ability (item effect bit 4 for critical, 5 for backstab), and the result is never worse than that term alone | **decompiled source**, MODULE4.BAS:386 and 404 |
| Two ways a weapon simply does not work: 124 monsters (lycanthropes, elementals, demons, dragons) need a weapon from at least half their own floor, and four are immune to weapons outright. Both abandon the whole attack, and the weak-weapon case puts you on Defend | **decompiled source**, MODULE4.BAS:347-363, with `sub102F ... gc1DB4` being the switch to Defend |
| Attacking bare-handed is not an attack at all: "Using no weapon!" and you are put on Defend | **decompiled source**, MODULE4.BAS:327 |
| Extra swings are earned from the Multiple Swings guild ability at doubling level thresholds, and only five of the twelve guilds have the ability at all - the first extra swing lands at guild level 55 for a Ninja, 90 for a Warrior | **decompiled source**, FRMCTYG.BAS:394-406 |
| The character you are watching has their swings drawn a quarter of a second apart, pumping messages in between, so a long attack takes longer than a second and simply delays the next round | **decompiled source**, MODULE4.BAS:366 `sub07E2 .25#`, whose delay is live whenever gv1B50 is false - which is all of normal play (FRMDUNM.BAS:302) |
| The monsters resolve in exactly two places: the one-second timer, and one free round when you change square or level while they are engaged. Nothing else you do gives them an extra swing | **decompiled source**, MODULE7.BAS sub15B0 (the timer) and MODULE3.BAS:1462, which stops the clock, calls sub0A64 once and starts it again |
| You cannot see a monster's hit points. The list shows a number, then - only for a monster that comes in groups of two to twelve - how many are still standing, then its name | **decompiled source**, MODULE4.BAS:4392-4398 |
| A monster is named through `fn0E23` wherever it is named at all: its own name, or its class if you have not identified it, or "Unseen Monster" if it is invisible and you cannot see invisible - and pluralised by the original's own table of irregulars. **Corrected 2026-09-22**: this row used to begin "Every line of combat text names a monster through fn0E23", which is not so. Every caller of `fn0E23` is an identification line, the Seer or the Library (FRMDUNM.BAS:467-490, FRMSEEK.BAS:73, HALL.BAS:117). The melee captions name nothing at all - see the entry below | **decompiled source**, `fn0E23` (MONSMOD.BAS:2274-2320) |
| A group is created already identified; meeting them in fog is what leaves you guessing, and then you roll each ailment tick to work out what they are | **decompiled source**, MODULE4.BAS:1879-1882, where the fog flag is bit 20 |
| Standing orders: None, Fight, Defend, or a spell that is recast every round | **decompiled source**, `gv1E66(i)` - `.Att` 0/1/2 (FRMGROUP.BAS:454-456), `.M56C4` holds the queued spell as id + 1000 (MONSMOD.BAS:310-317) |
| Defend raises your defence by half | **decompiled source**, MONSMOD.BAS:2688: `If gv1E66(i).Att = gc1DB4 Then l048C = l048C + (l048C * .5)` |
| Monsters do not attack on sight. They stand on their square and roll every second whether to come for you: `base − 7.5·Log(CHA)`, base from the alignment pairing (a Good monster never starts on a Good character, and goes straight for an Evil one at 100) | **decompiled source**, MULTIMOD.BAS:559-587 |
| You can start the fight yourself, and usually do - giving a Fight order or casting at monsters who have not moved engages them | **decompiled source**, MAINMOD2.BAS:4025-4028 and 1408: `If Foes(0).M4ECC = 0 Then Foes(0).M4ECC = 1` |
| Each character has a Combat Setting - the order they take up when a fight starts - set in Party Options, and **stored on the character** so it survives a save | **decompiled source**, FRMPINFO.BAS:51-59 writes `Chars(i).M5914(5)`: 0 Nothing, 1 Fight, 2 Defend, 3 or more naming spell buffer `value - 2` |
| **Spell buffers**: ten slots per character, each holding either a pack slot or a spell. Below 40 is the pack slot; 40 or more is the spell at that index. A buffer pointing at something that has gone resets itself to empty | **decompiled source**, `Chars(c).M59B0(0..9)` and `mdcharShowBuffer` (MAINMOD2.BAS:4633-4663), bound by dragging an item or spell onto one (FRMCHAR1.BAS:218-231) |
| Shift and a number fires buffer 1 to 10, the 0 key being the tenth. An item buffer uses that pack slot; a spell buffer becomes your standing order while monsters are engaged, and is cast once otherwise | **decompiled source**, MAINMOD2.BAS:4138-4158 |
| A **plain** number key, 1 to 4, picks which monster group you are aiming at | **decompiled source**, MAINMOD2.BAS:4131-4136: `CombatOrders(Me).M547A = key - 49` when that group still has monsters |
| Only minimising the game stops the clock. No dialog, menu or window pauses combat | **decompiled source**, `gv1ACC.M602F` is the flag both timers check (FRMDUNM.BAS:431, 617) and FRMMAIN.BAS:213/225 set it solely on `WindowState` |
| The maze and the monsters share one screen; combat never covers the view | **decompiled source**, frmDungeonMain carries picMaze2, picMonsters, picMonGroups, lblMonster, lblCurrentGroup, lblCharacterHit, lblMonsterHit and lblSquareInfo together |
| **Every timer interval in the game.** `timBlinker` 500 ms, `timCombat` 1500 (the code resets it to 1000 on entering the dungeon), `timAnimate` 2000, `timAilment` **4000**, `timMusic` 10000 | **the decompiled forms**, recovered from the binary `.FRM` files - see [DECOMPILE.md](DECOMPILE.md#recovering-design-time-properties) |
| Poison and disease tick on `timAilment`, every four seconds, not on your steps. Poison takes 2 hits a tick and shakes off on `Rnd(200) + (CON−10)×2 − poisonLevel×2 > 185`; disease takes 1 and needs > 195. Dying of old age is checked on the same clock | **decompiled source**, FRMDUNM.BAS:534-556 and 567-577 |
| Water and quicksand cost you hits equal to the dungeon level every tick, and quicksand also destroys a random pack item on `Rnd(100) < (level/25)×100`. Levitating keeps you clear of both | **decompiled source**, FRMDUNM.BAS:500-528 |
| The dungeon makes noise: a flat 10% each tick, but only with nothing in front of you, playing one of SCURRY, MOAN or DSTSLAM | **decompiled source**, FRMDUNM.BAS:449-453, sound ids 8-10 from MODULE1.BAS:312-314 |
| You identify monsters over time rather than on sight: `((INT+WIS)/2) × Log((topGuildLevel+5)/10 + 1)`, +20, clamped 10..90, rolled per group per character per tick; darkness costs a quarter of it | **decompiled source**, `mdrcharGetIDSkill` (MAINMOD2.BAS:2502) and FRMDUNM.BAS:455-497 |
| An invisible monster reads as "Unseen Monster(s)" to anyone below ground without See Invisible; an unidentified one reads as its class ("Humanoid", "Undead") | **decompiled source**, `fn0E23` (MONSMOD.BAS:2279-2301) with the class table `gv1662` out of MDATA1 |
| **MDATA1's layout, exactly.** Record 1 is the version, records 2-7 are six counts, then races, guilds, item types, item descriptions, monster classes and monster groups follow one per record. The port used to find races and guilds by scanning record ranges and keeping what looked plausible | **decompiled source**, `fn03D0` (MODULE1.BAS:678-716) reads the file straight through with record-less Gets, so the order is the layout |
| An unidentified item is named by a ladder, not a placeholder: the description ("Weapon", "Round Object"), then the type ("Dagger"), then its own name, and only when completely identified does it carry the alignment word, charges and "(cursed)" | **decompiled source**, `fn1345` (MAINMOD2.BAS:2566) over `gv1592` and `gv15C6`. This replaced an invented "? Unknown {type}", and the port's hand-written type list was wrong in five places - the game says Chain Armor, Plate Armor, Belt, Miscellaneous Item and Guild Crest |
| The four identification levels are named "Completely Identified", "Mostly identified", "Partially identified", "Barely identified" | **decompiled source**, `fn132C` (MAINMOD2.BAS:2553) |
| Teleport and Displacement take offsets **relative to your facing** - right and forwards, not east and north - and stepping off the 30x30 grid wraps to the other side rather than failing. Teleport's third number is depth, clamped to three levels either way. Nothing checks the far end: land in rock and the whole party dies | **decompiled source**, `mdrcharTeleport` (MAINMOD2.BAS:5738-5836); the help warns "if the Leader of the party casts Teleport and lands in Rock, all members of the party will also land in Rock" |
| Raising a dead companion is `min(98, 85 + the monster's CON)`, and a failure destroys it outright; only Resurrect brings anything back at full health, the rest leave it on one hit | **decompiled source**, MAINMOD2.BAS:1077-1087, with effect 22 as the full-health case |
| Soul Search reports another character as "right N, forward M, and down K" from where you stand and the way you face, refuses anyone more than three levels away, and says "is in the city" for anyone above ground | **decompiled source**, MAINMOD2.BAS:1279-1304. It is the exact inverse of the Teleport transform, and 1,620 round trips through both agree |
| Retrieve Soul only reaches someone on your own level | **decompiled source**, MAINMOD2.BAS:1319. The test is `same level Or deeper than 15`, and 15 is the whole dungeon, so the second half can never fire |
| **Carrying a body out.** One at a time: a fallen character or one of your own dead companions. The body is not moved on every step - its position is written each time the carrier enters a new area or level, and when you leave the dungeon, so it ends up wherever you did. Walk out and it is in the city for the morgue | **decompiled source**, `M5943` on the character (-1 nothing, 0-3 a companion, 4 or more a character that far into the morgue list), picked up at FRMMORG.BAS:59, delivered by `sub0A7C` (MAINMOD2.BAS:5974) from `mdrmazShowMaze` on each change of area or level (MAZEMOD.BAS:839-841) and from the dungeon form's load (FRMDUNM.BAS:284-285), handed over at FRMCTYM.BAS:121-128. The port had delivered only as the window closed (corrected 2026-09-27) |
| You cannot raise the dead while carrying someone | **decompiled source**, MAINMOD2.BAS:1062: "drop who you're carrying to raise the dead" |
| Resistances: race base, then a flat **90** if any equipped item of your own alignment resists that kind, then whatever a spell has raised it to if higher | **decompiled source**, MAINMOD2.BAS `sub120F` (gathers gear, mc031A = 90), `sub1447` (race base) and `sub0894` (combines). This replaced two guesses that disagreed with each other - the dungeon added 50 per item and combat treated any one item as 100 |
| Item and spell effects: six bits (Levitate, Invisible, Protect, See Invisible, Critical Hit, Backstab) feeding the four states the sheet shows | **decompiled source**, MAINMOD2.BAS `sub0894`, names from MODULE1.BAS:518-523 and 442-448 |
| All ten chest traps, including which hit the whole party and which only the character who opened the chest | **decompiled source**, MONSMOD.BAS `monOpenChest` lines 3516-3634 |
| What every spell *does*: MDATA2 carries an effect id, and the game dispatches on it. 3 Levitate, 4 Invisible, 5 See Invisible, 6 Protect, 7 Teleport, 9 Passwall, 11 Detect Rock, 12 Depth Perception, 13 Find Direction, 14 Locate, 15 Fetch, 16/17 Sanctuary, 19 Fate, 20-25 raise, 30-32 open, 40-42 bind, 50-60 resist, 150 bind for life, and +200 for the whole party | **decompiled source**, MAINMOD2.BAS lines 855-1353. The field had been read one word late, so every spell looked like effect 0; reading it in the right place makes all 105 spells line up with their names (effect 3 is Feather Essense, 9 is Ethereal Portal, 150 is Life Domination, 203 is Mass Levitate) |
| Cure spells are recognised by effect id, not by a guessed spell id | **decompiled source**. The four ids previously hard-coded (19, 33, 88, 57) turned out to be exactly the spells the effect test finds, so the behaviour is unchanged and now sourced |
| Companion binding: starts at `min(5 + floor, 10)`, the Confinement raises it 4 at a time to 12, cost `guildLevel * 1237 * next * current / 4`, and the chance of a companion leaving | **decompiled source**, FRMCITYC.BAS:136-138 and `fn33B9`; MONSMOD.BAS `fn0D78` and `fn0D92` |
| Races, guilds, requirements, alignment and race masks, hits per level, experience factors | MDATA1 |
| Guild spell tables (which spells, at which level) | MDATA1 |
| Guild abilities (thieving, backstab, crit, swings, fighting, perception) | MDATA1, cross-checked against the help's table |
| Spells, items, monsters, all 15 dungeon levels | MDATA2, MDATA3, MDATA5, MDATA11 |
| Monster pictures | sprite sheet in MG11.DLL, indexed by the data's PicId |
| Guild rank titles (Vagrant, Bravo, Guard …) | string table in MORDOR.EXE |
| Experience needed per level, with the race's Exp | **decompiled source**, `fn12A1` (MAINMOD2.BAS:2118-2138; `Rules.Fn12A1`). Experience_Required_for_Level_Formula agrees (133 for level 2, matches a screenshot; its anchors are in selfcheck) |
| Experience awarded per point of damage | **decompiled source**, `sub0CDF` (MONSMOD.BAS:174-181) and the party factor in `sub0F11` (MAINMOD2.BAS:289); `Rules.ExpAwarded`. Experience_Awarded_Formula states it tidied up |
| Gold to make a level; cost to join a guild | **decompiled source**, `fn12B2` (MAINMOD2.BAS:2140-2155) and FRMCTYG.BAS:115-127; `Rules.Fn12B2`, `Rules.GuildJoinCost`. Gold_Required_for_Level_Formula and Guild_Join_Cost_Formula agree (all 12 join costs to the gold piece) |
| Attack and defence from stats; guild A/D tables | **decompiled source**, `mdrcharSetAD` (MAINMOD2.BAS:4455-4548) for stats, and `adInterval` (MAINMOD2.BAS:2680-2708) with modSetNewAD's award (FRMCTYG.BAS:1019) for guilds; `Rules.StatsAD`, `Rules.GuildAD`. Stats_A-D_Formula and A-D_For_all_Guild_Levels agree (all 334 published values, `adcheck`; 10/10 for a level-1 Nomad matches a screenshot) |
| Chance to hit, melee damage, crits, backstabs | **decompiled source**, `fn0DEE` (MONSMOD.BAS:2217-2263), one routine for both sides, called from `sub1019` (438), a companion (1157) and a monster (2951); crits and backstabs MONSMOD.BAS:381-420; `Rules.Fn0DEE`, `CharacterDamage`, `CritChance`, `BackstabChance`. Character_Attack_Formulas and Monster_Attack_Formulas describe the same |
| Whether a monster group gets its free attack when you move - the page calls this initiative, but it is not one | Combat_Initiative_Formula, and **decompiled source** `sub0A64`, MULTIMOD.BAS:283-360, which settles the page's "[maximum 98?]" |
| Spell cost, spell damage, healing | **decompiled source**, `fn1485` (MAINMOD2.BAS:2868-2903; `Spellbook.Cost`, `Rules.Fn1485Price`), `monGetSpellHits` (MONSMOD.BAS:2464-2533; `Rules.SpellDamage`) and the heal branch of mdrcharCastSpell (MAINMOD2.BAS:952-961; `Rules.SpellHeal`). Spell_Cost_Formula and Spell_Damage_Formula agree (max damages match the published observations) |
| Maximum spell points = (INT + WIS) x 5 | **decompiled source**, `maxSpellPoints` (MAINMOD2.BAS:2636-2638), natural stats; the help agrees, verified against a screenshot (100 at INT 10 / WIS 10) |
| Starting stat points = 30 + race bonus, five pre-spent per stat | verified against a screenshot (Human 9/9/9/11/10/11 with 5 left) |
| Starting gold 1,500, age 16, hits 15 | screenshots |
| Monsters live in rooms; the entry room is safe; rooms repopulate | help; the repopulation is **decompiled source**, `sub0A91` (MONSMOD.BAS:691-716; `DungeonState.RespawnDelay`, `RefillDue`) |
| Monster kinds per room and lair monsters | MDATA11 spawn areas |
| Item alignments (U/G/N/E) and which items can carry which | MDATA3 AlignmentFlags |
| The city heals and ages you on arrival | help |
| Automap records only squares you walk; grey edge until redrawn | help |
| A square drawn small with grey around it means *mapped since the map was last plotted in full*, not *newly discovered*: right-clicking the map or switching level fills them all in solid | help ("If you wish to redraw the Automap ... you can Right Click on the Automap, or switch to another level"), and **decompiled source** - `medPlotMaze` fills a box a pixel proud of the square on every side (FRMAUTOM.BAS:421) where the incremental draw fills one inset by four (232) |
| Walking with the map open moves the arrow and draws the new squares in | **decompiled source**, `frmAutoMap!lblCall = "DRAW"` at the end of every recorded step (MAZEMOD.BAS:1692). The port left the map frozen at whatever it showed when you opened it, arrow and all |
| The automap is told only what posts to it - steps, turns, spells, the Options window - and never by the ailment clock | **decompiled source**, `timAilment_Timer` (FRMDUNM.BAS:395-614) never names frmAutoMap; its one call out is `sub0BF1`, frmParty's "STATS" (MULTIMOD.BAS:601-603). The port's ailment tick ended in the Redraw that posts "UPDATE", and with "Auto Change Lev." medDrawCursor turns hsbLevel to the level you stand on (FRMAUTOM.BAS:306-313): a level paged to on the map jumped back to your own within four seconds - reported as "resizing the automap resets it to level 1". Fixed 2026-10-14 |
| Paging the automap's levels never resizes the window | **decompiled source**: hsbLevel is a VB3 scroll bar, which has no DblClick, and hsbLevel_Change only re-plots (FRMAUTOM.BAS:194-201); only picMaze_DblClick, Form_DblClick and picPicture_Click toggle the size (51-53, 585-606). The port's arrows are buttons, and a quick second click on one raised Avalonia's double tap, which bubbled to the child and toggled it to the default size. Fixed 2026-10-14; the sizing grip is excluded too, being the window's border and not the form |
| Chance of losing CON on a raise; complications | **decompiled source**, `raiseComplications` (MAINMOD2.BAS:1642-1685; `Raising.Complicates`) and the morgue's `Rnd * 100 < (50 - CON * 2)` (MAINMOD2.BAS:358, 3113; `Rules.RaiseConLossChance`). Chance_of_Complications_Formula agrees |
| Items cast their spell at the item own SpellLvl, not the wielder level - which is why a wand is as strong in a novice hand as a master one | **decompiled source**, MONSMOD.BAS monGetSpellHits |
| Every special attack gate: ageing and stat drain share a CON * 2.5 save, paralysis saves on resistance + CON capped at 95, item destruction is a DEX save on one random pack slot and only reaches gear from about the monster depth, gold theft builds a thieving score and steals on a 3353.452 span, monster criticals and backstabs scale on guild level against yours | **decompiled source**, MONSMOD.BAS - each gate read individually |
| Petrification, poison and disease in combat: stone is two saves, first the resistance then CON * Log(CON)/Log(2.5) minus the monster floor capped at 95; poison and disease are one roll against the matching resistance and never stack on someone already suffering | **decompiled source**, MONSMOD.BAS |
| Companion limit: four slots, flat | **decompiled source**, MAINMOD2.BAS fn117A over a fixed array of mc00E0 = 4 |
| Charming: a charm spell is cast like any other and a monster it reduces below one hit joins you instead of dying, provided a companion slot is free | **decompiled source**, MAINMOD2.BAS |
| Quests: never below guild level 5, chance is the guild own QuestPercent, a 5% reprieve, fetch quests only above level 15, and the level is refused until the quest is done | **decompiled source**, FRMCTYG.BAS |
| Item use level: (factor + factor * 2.97) * item.LevelScale, at least 1, where the factor is the guild own - Nomad 0.8 soonest, Healer 1.5 latest | **decompiled source**, MODULE1.BAS fn030F and MAINMOD2.BAS fn11B6 |
| Encounter composition: up to four groups, the first the monster rolled and the rest like-types that travel in company, each group InGroup less up to half, and every monster hits varying by a quarter | **decompiled source**, MONSMOD.BAS |
| Item drops: the chest yields an item on the monster own ItemChance nudged by treasure class, drawn from the ten item kinds that monster carries, within a floor band from its ItemDropLevel, weighted by Rarity; charges vary by up to half | **decompiled source**, MONSMOD.BAS |
| Identification on pickup: skill from INT+WIS with gear and top guild level, against a difficulty built from the item own floor | **decompiled source**, MAINMOD2.BAS fn12F0 and mdrcharGetIDSkill |
| Poison and disease per step: a cure roll of Int(Rnd*200) + (CON-10)*2 - poisonLevel*2, needing over 185 for poison and over 195 for disease, costing 2 and 1 hits when it fails | **decompiled source**, FRMDUNM.BAS |
| Guild titles: the exact level bands per guild, which are uneven and widen with level | **decompiled source**, MAINMOD2.BAS fn12C4 |
| Morgue revival cost: 2912.32 x (MaxHits / 1.5) | **decompiled source**, MAINMOD2.BAS fn1416 |
| The shelf a new game starts with, and other adventurers restocking it while you rest | **decompiled source**, MAINMOD.BAS: the initial stock rule and mdrReplenishStore, which mdrcharAddAge calls with the days you just aged. Reproduces the real game opening store exactly - Bronze Dagger 250, Iron Dagger 0 1 1 1, Dagger of Stealth 0 0 1 0 |
| Chest traps: the eleven-entry trap table, which chests are trapped (25/75/90 percent by treasure class), and disarming by the party best thieving ability | **decompiled source**, MONSMOD.BAS monOpenChest and MODULE1.BAS |
| Guild abilities: the real per-ability chance, opposed by dungeon level and the monster guarding the chest | **decompiled source**, MAINMOD2.BAS fn122A - the community thieving page turns out to be this with the opposition term folded into a constant |
| Guild A/D: every character starts 10/10 and a guild awards its own fractional Att/Def only on levels where level Mod Int(AdEvery) is zero, accumulated in a Single and shown as its whole part | **decompiled source** + MDATA1 fields 17-20. Reproduces all 334 published values, every level of the five guilds the community tabulated completely, above each guild's cap as well as below; covers all 12 guilds |
| Store: sell price, identify fee, realign cost, uncurse fee, charge-chunk selling | **decompiled source**, FRMCTYS.BAS fn273B, fn271B, cmdRealign_Click, fn274F, cmdSell_Click |
| Aging: age is kept in days from 16*365, resting costs (100 - Hits/MaxHits*100)/3.25 days, and past 98% of your race lifespan there is a 5%+Log(excess) chance a turn of dying of natural causes | **decompiled source**, MAINMOD2.BAS mdrcharAddAge, CHARCREA.BAS, FRMDUNM.BAS |
| Special-attack *damage* (breath, acid, electrocution): one shape for every kind, differing only in a /4 or /2 level term, with resistance half effective and applied cumulatively down a line of companions | **decompiled source**, MONSMOD.BAS:2710, 2779 and 3060 - written out inline in `sub0E85` rather than shared, so each differs slightly. The community Acid page matched the shape; the Breath one did not |
| Monster gold (encounter treasure, chest class, locked chests) | **decompiled source**, MONSMOD.BAS: the generator builds it group by group from GoldFactor and Floor, scales by the party size and the chest class rolled from BoxChance, and monGetGold splits it |
| Store buy price (modItemCost) | **decompiled source**, FRMCTYS.BAS - and verified against the real game running under OTVDM: Good Iron Sword, 1 in stock, predicted and printed 73,823 |
| Store: UGNE counts per alignment, fixed prices for store-only goods, bold for what you can use, tan-yellow input vs dark-grey display fields, search on return | help, "The City: Store" |
| Combining is for items with charges only; uncursing identifies the item completely first; shelved items come back fully recharged | help, "The City: Store" |
| City services take the shortfall from your bank account | help, "The City: Store" |
| Exchange: passing gold between two characters - below ground only what is on hand, in the city the bank makes up the difference | **decompiled source**, FRMEXCHA.BAS:92-128 |
| Exchange: swapping two pack slots, refusing guild-issued gear on either side and anything worn when the two slots belong to different people | **decompiled source**, FRMEXCHA.BAS:156-214 |
| Identification by reading: 25 + (Log(DamageMod+1) + 0.3) x Log(LevelScale+1) x Floor against the holder's identify skill; within skill and Floor<=5 reads Mostly, twenty short reads Barely, everything else Partial | **decompiled source**, MAINMOD2.BAS:2480 fn12F0 |
| Only the character dropped *onto* re-reads what they were handed; the other side keeps whatever it knew | **decompiled source**, FRMEXCHA.BAS:199 - the handler runs on the drop target and looks at that slot alone |
| Guild log: MDATA9.MDR, one shared file, each hall showing only its own entries newest first, dated TODAY / N DAY(S) AGO | **decompiled source**, FRMGLDL.BAS sub318B and the writer MODULE11.BAS:25 |
| Guild log: Clean appears only once the log passes fifty entries a guild, keeps the newest fifty of all of them, and optionally prints the rest to GUILDLOG.TXT | **decompiled source**, FRMGLDL.BAS:44 and sub316B |
| The eighteen sub0578 call sites, which between them write sixteen distinct lines (his/her and party/alone are the two variant pairs), word for word - counted, and the call-site list and every wording checked against the .BAS files by `selfcheck` | **decompiled source**, CHARCREA, FRMCTYG, MODULE2/3/4/6/11 |
| "You are Alone" only when the party is one; otherwise "Character N of M" | **decompiled source**, MODULE6.BAS:5636 |
| Being lost: three separate flags - the floor, the square, the facing - each hiding its own part of the readout, with the automap neither recording nor showing you while any of the first two hold | **decompiled source**, MODULE3.BAS M59F4, set at 118/1729/1816, and FRMAUTOM.BAS:303-333 |
| A chute or a teleport onto a square you have never mapped loses all three | **decompiled source**, MODULE3.BAS:116 and 1815, both guarded by fn0A34 - is this square on the map? |
| A rotator square gives you an outright random facing, Int(Rnd*4)+1, and takes the square and the facing unless perception beats Rnd*70 + dungeonLevel*2 | **decompiled source**, MODULE3.BAS:1727 |
| Working it out again: Log(perception)^2 over 15 for the floor, 20 for the facing, 25 for the lot, each against Rnd*100, every step | **decompiled source**, MODULE3.BAS:1541-1562 |
| Walking onto ground you have already mapped ends it outright - "You've found yourself!" | **decompiled source**, MODULE3.BAS:1565 |
| Reaching the city clears the floor and the square but not the facing | **decompiled source**, FRMCTYM.BAS:87 |
| Guildmasters: each guild records its highest-ranking member; you take the chair when your level passes theirs, and matching it is not enough | **decompiled source**, FRMCTYG.BAS:434 |
| The twelve named masters a new game installs - Crashland 64, Garth SliceMonger 56, Eli 62, Shogun 48, Maxx 67, Martin 77, Nimblefingers 56, Kane the Wise Axe 54, Sparrowhawk 52, Requnix 61, Merlin 48, Hands on Henry 65 | **decompiled source**, MODULE2.BAS sub0565 |
| The guild crest is the item whose id is 299 plus the guild index, awarded fully identified, skipped if you already hold one, and lost if the pack is full | **decompiled source**, FRMCTYG.BAS sub29DF (its own error handler calls it modAwardCrest); MDATA3 has all twelve at ids 299-310 |
| Forfeiting a quest: offered only to a character who is pinned, costs Int(Log(level + 1)) levels, clears the quest, and unequips anything the lower level no longer allows | **decompiled source**, FRMCTYG.BAS:271-296 and sub29BD |
| Pinned: experience has reached fn13ED, one short of what the level after next would need | **decompiled source**, MODULE6.BAS:2705 |
| A level makes you sharper, so the whole pack is read again and any item you now make more of is announced by slot number | **decompiled source**, FRMCTYG.BAS:422 |
| Raising: stone yields only to Restore Flesh, and a character pulled into solid rock only to Resurrect - the morgue will not touch the latter at all | **decompiled source**, MODULE6.BAS fn142D:2743 and FRMMORG.BAS:66 |
| A raise spell's chance is 35 x (CON / 4.5) + tier x 10, the tier counting from Raise Dead as one, clamped 10..98; rock overrides it with a flat 10 | **decompiled source**, MODULE6.BAS:2762-2781 |
| Teleporting into solid rock kills the whole party with that same cause, which is what makes it so hard to come back from | **decompiled source**, MODULE6.BAS:1179-1189 |
| Ability ratings: the raw ability placed between the weakest and strongest anyone could be at it, never below 1 - and with no upper clamp, so a Seeker's perception passes 100 | **decompiled source**, MODULE6.BAS fn123F:1982, fn13AA:2635 and fn135B:2590. Reproduces the one screenshot value (a level-1 Nomad with DEX 11 WIS 10 rates 3) and the help's worked example that only the Warrior reaches 100 fighting |
| The Hall of Records: twelve town-wide bests in two columns, each naming the holder, the date and what they did; an unclaimed one credits the "Original RecordHolder" | **decompiled source**, FRMHALL.BAS sub32F6 |
| A record changes hands only on a strictly greater value; improving your own is silent, and the creature record is never announced at all | **decompiled source**, MODULE11.BAS sub1043 - the same flag serves both cases and the sub exits on it |
| Monster power: Att x2 + STR x10 + Hits/10 + DEX x2 + guild level, plus 75 a special attack and 100 a spell | **decompiled source**, MODULE11.BAS fn1BF0:546. Puts Asmodeus top at 5,469 |
| Combined fighting skill, total thieving skill and spell knowledge & power, as the Hall measures them | **decompiled source**, MODULE11.BAS fn1BF0:561, 573 and 590 |
| When each record is checked: six stats at creation, the three skills after a level, gold at the bank, experience on reaching the city, the creature on every kill | **decompiled source**, the sub1043 call sites in CHARCREA, FRMCITYB, FRMCTYG, FRMCTYM, MODULE4 and MODULE6 |
| Stud squares (flag bit 22) draw the encounter from one floor deeper and usually narrow the window to one, which is the only way the ten monsters filed at floor 16 - Asmodeus among them - can ever be met | **decompiled source**, MODULE4.BAS:1432 and 1491; the community's MDATA11 format notes name bit 4194304 "Stud", and the FAQ describes fighting level-12 monsters from level-11 stud rooms |
| The encounter window reaches back from the target floor by a spread of 1 or 0, plus floor/5 rounded, capped at the floor | **decompiled source**, MODULE4.BAS:1481 |
| Defeating Asmodeus: the ending, and a guild-log line for every survivor - "single handedly" alone, "part of the party that defeated" otherwise | **decompiled source**, MODULE4.BAS sub0C61:4234 |
| Asmodeus announces himself with his own sound, and his chest draws from the top three floors of his range rather than the usual half-band | **decompiled source**, MODULE4.BAS:1644 and 3697 |
| One damage function, `fn0DEE`, serves every blow on both sides: it rolls to hit inside itself and answers 0 for a miss, so "miss" and "did no damage" are the same event | **decompiled source**, MONSMOD.BAS:2212, called from MODULE4.BAS:433 (characters) and 2932 (monsters) |
| A character swings with one weapon at most. The swing loop walks two hands, M5997(0) and (1), but sub0F74 fills them only for wear class 0 and every equip passes fn11B6's limit of one of that class, so the second hand stays empty; `PlayerSwings` walking every equipped weapon is the same thing, and so is the "Using no weapon!" sum (checked 2026-10-19) | **decompiled source**, MODULE4.BAS:320-337, MAINMOD2.BAS:1728, 1781, 1842-1848; MDATA1 types 0-7 are wear class 0 with M54F1 = 1, and no weapon is a self-attaching curse; selfcheck "the weapon hands" |
| The to-hit threshold is 2..98 and the miss test is `Int(Rnd * 100) + 1 >= threshold`. A character swings at a monster defence of 0 - the original passes an unassigned local - so the blow lands 97 times in 100 | **decompiled source**, MONSMOD.BAS:2221-2236 and MODULE4.BAS:433 |
| The attack/defence term is subtracted, not added: `Int(Int((Def - Att) / 2) / 4)` less a random share of itself and one more, taken off the damage 999 times in 1000. With defence 0 that is a small *bonus*, which is what the community's "+ Rnd[1 to Atk/8]" is really describing | **decompiled source**, MONSMOD.BAS:2250-2253 |
| The hit captions, word for word: "You missed!", "You hit for N damage!", "You Killed One!", and for the specials "You Critically hit for N damage!", "You Backstabbed for N damage!", "You Severed one for N damage!" and their kill forms | **decompiled source**, MODULE4.BAS:436, 453-460 and 481-489 |
| Those lines appear only for the character you are watching - `If pv00F4 = gv1A86` guards every caption. Another party member's blows make a sound and change the monster count, and say nothing | **decompiled source**, MODULE4.BAS:364, 496 |
| The game waits a quarter second twice around every swing of the watched character: once with the caption cleared before the blow is rolled, once with the result showing. It does not wait for "Using no weapon!" or "Weapon has no effect!" | **decompiled source**, `sub07E2 .25#` at MODULE4.BAS:366 and 499 |
| Each swing has its own sound: SWING for weapon types 0, 1, 2, 4, 5 and 7, SLICE for the rest; then CHIT, BSTAB or RIP for a critical, backstab or sever, HIT for any landed blow, and the monster's own death sound when it falls | **decompiled source**, MODULE4.BAS:340-345 and 441-447; death sounds `sub0CB7`, MUSIC MODULE9.BAS:94 |
| Which group of monsters attacks whom: the front of the party draws the fire, weights 100, 50, 25, 12, 6 down the line, each group rolling once | **decompiled source**, `sub0A64` MULTIMOD.BAS:279-356 |
| Monsters get a free attack when you move or turn on their square and when you arrive on the level, but only if a roll beats `50 + (their DEX - your DEX) * 5`, floored at 5 | **decompiled source**, the `sub0A64` call sites: FRMDUNM.BAS:335, MAZEMOD.BAS:813 and 1466, MONSMOD.BAS:589 |
| A group's turn, in order: breath, then acid, then up to five of its members each cast or swing, then electrocution, ageing, drain, stoning, poison, disease, paralysis, item destruction and theft | **decompiled source**, `sub0E85` MONSMOD.BAS:2588-3406 |
| The monsters' own captions: "N <name> hit for D!", with " delivered a Lethal", " Critically" or " Backstabbed and" spliced in; "Monster missed." / "Monsters missed."; "Monster cast \"X\" for D damage!" and its failure form; and the specials' own lines - "You were Breathed on for N!", "You were Spit on for N!", "You were Electrocuted for N!", "You are now older!", "You have been drained!", "You are now a statue.", "You have been Poisoned!", "You have been Diseased!", "You have been Paralyzed!", "N Gold Stolen!" | **decompiled source**, MONSMOD.BAS:2740, 2807, 2974-2999, 3076, 3126, 3164, 3201, 3245, 3261, 3280, 3340 |
| A monster picks its spell by school from its SpellFlags, then a spell of that school between two thirds of its floor and one above; a tenth of the time nothing comes, and anything not of the Mage class gives up half the time again | **decompiled source**, `fn0FF5` MONSMOD.BAS:180-243 |
| A caster's level is its guild level over four; the spell's own last field says which resistance the target rolls against, effect 100 means the spell only counts if it would kill, and effects 75..99 mean it only touches one class of creature | **decompiled source**, `fn0E5F` MONSMOD.BAS:2459-2560; confirmed against MDATA2, where Flesh to Stone carries resistance 7, Leprosy 4, Poison 5 and Acidic Spray 10 |
| No monster casts on an anti-magic square or an extinguisher square | **decompiled source**, MONSMOD.BAS:2836 testing flag bits 18 and 10 |
| A monster that casts *replaces* the turn's damage total rather than adding to it, so a melee blow landed earlier in the same turn is discarded and never reaches the character - the damage is applied once, after the loop | **decompiled source**, MONSMOD.BAS:2839 assigns where 2956 adds |
| A drain attack can take a point off a stat or two off your maximum hits, one case in six - and because the draw is `Int(Rnd * 6 + 1)`, Strength is unreachable | **decompiled source**, MONSMOD.BAS:3142-3168 |
| Extra swings arrive at the levels the community table publishes - Ninja 56/122/267/585, Warrior 91/324, Villain 105/434, Paladin 111/484, Scavenger 116/531 - because the threshold is a VB `Integer` and is rounded at every doubling | **decompiled source**, FRMCTYG.BAS:184 declares it `As Integer`, 395-402 recompute it; matches `Extra_Swings.txt` exactly |
| Attack and defence read the stats **as equipped gear leaves them**, not the natural stats: a Girdle of Strength is worth its bonus in A/D | **decompiled source**, `sub047C` MAINMOD2.BAS:4460 onward reads `M55B5(i) + M584B(i)`, and MAINMOD2.BAS:1761-1773 recomputes A/D on every equip |
| The Defend order's +50% defence is **dead code** in the original and the port does not apply it: `l048C` is reassigned at MONSMOD.BAS:2906 before its only use at 2946. Defending still stops you swinging and still makes you harder to rob | **decompiled source**, MONSMOD.BAS:2682-2684 vs 2906/2909/2946 |
| A critical or backstab is rolled against a fractional chance, not a whole percent - `Int(Rnd * 100)` against a `Single` | **decompiled source**, MODULE4.BAS:394, `Dim l010C As Single` |
| Hits on making a level: below the guild's ML you get AH flat, plus two times in three a slice of 1..AH/5, plus - only below ML, and only with CON over 16 - another 1..CON-16. Past ML you get exactly MH with no roll. Maximum hits stop at 999, and a level fills you up | **decompiled source**, `cmdMake_Click` FRMCTYG.BAS:347-375 |
| You gain **no hits at all** for a level another guild has already reached, which is what stops a second guild paying twice for the same ground | **decompiled source**, the `l0096` gate, FRMCTYG.BAS:347-357 |
| Forfeiting a quest *assigns* your experience to the floor of the level you fell to, so the banked experience that pinned you is gone and every lost level must be earned again | **decompiled source**, FRMCTYG.BAS:942-944 |
| Experience is clamped at the pin cap as it is awarded, with "You are now pinned." said once | **decompiled source**, `sub0F11` MAINMOD2.BAS:285-296 |
| The Questmaster will not send you after an item worth under 100 gold, a cursed one, one found on level 0 - which is what keeps the guild crests out - or, three times in four, a rare one | **decompiled source**, FRMCTYG.BAS:552-560, matching `Questing_Formula.txt` |
| A monster's minimum item-drop level rounds half to even, giving the published 1,1,2,2,2,3,4,4,4,5,6,6,6,7,8,8 for drop levels 1 to 16 | **decompiled source**, MONSMOD.BAS:3677 lands the division in an `Integer`; matches `Item_Generation_Formula.txt` |
| A companion's blow is one call to `fn0DEE` - no separate to-hit roll, no floor of one damage, no size term - and its critical is worth **+4** where a monster's against a character is +2. The critical and backstab are bits 10 and 11 of its **special attacks**, not its properties | **decompiled source**, MONSMOD.BAS:1085-1167 (`testBit(mc023C, ...SpecialAttackFlags)`, mc023C = 10) |
| A companion's blow, and the stone and electrocution riding on it, can land on a party member or one of their companions - on the spit's mishap odds, whether or not the companion can spit | **decompiled source**, MONSMOD.BAS:961-967 draw the victim before the spit bit is tested at 968; the blow aims at it at 1089-1097 |
| Chest traps that catch the whole party - the gas, the spores, the explosion and the fire - reach every member, not just whoever opened it | **decompiled source**, MONSMOD.BAS:3514, 3521, 3528, 3534 |
| A healing spell takes the spell level of the guild you are **in**, where a damage spell asks which of your guilds casts it best | **decompiled source**, MAINMOD2.BAS:943 reads `M58BB`; `Spell_Damage_Formula.txt:16` says so too |
| An area id indexes the 201 spawn-area records **directly**: id 0 is the first record, the "no area" one the rock cells carry | **decompiled source**, MONSMOD.BAS:1435 reads `levelHeader + 2 + width*height + areaId`, and MAZEMOD.BAS:1787 starts the teleporters at `+ 4 + width*height + 200`, which is exactly where 201 area records and a header leave off |
| A locked chest's half-again gold is applied **once**, on the one roll that also decides whether the chest is locked | **decompiled source**, MONSMOD.BAS:1692-1697 and 1971-1973 |
| A fight that leaves a chest pays **no gold at victory**; the gold is in the chest and is paid when it is opened. A chest left unopened, or one whose trap kills, pays nothing | **decompiled source**, MONSMOD.BAS:783-789 (`If Fight.TreasureClass = False Then l01D2 = monGetGold()`) and 3660 inside monOpenChest |
| Falling into a pit costs `Int(Rnd * (((level + 1) / 2) * 15.5)) * Log(raceSize + 2) + 1`, and a chute `Int(Rnd * (level * 10.5)) * Log(raceSize + 2) + 5`. Levitation carries you over a pit only when `5 + level/4 < Rnd * 100`, so deep pits catch you anyway; a chute it avoids outright | **decompiled source**, MAZEMOD.BAS:1748-1765 and 123-133 |
| Wading and quicksand cost nothing as you step - the water bites on the four-second ailment clock instead | **decompiled source**, FRMDUNM.BAS:500-528 is the only place either charges you |
| A group's size rounds half to even where the number of groups truncates | **decompiled source**, `CInt` at MONSMOD.BAS:1850 against `Int` at 1701 |
| The Seer locates creatures and items and does nothing else - he does not identify and does not uncurse, which are the General Store's work | **decompiled source**, FRMSEEK.BAS has three commands and no identify function at all; the help says "The Seer of Mordor has the ability to locate other characters, monsters and items that are in the depths of the dungeon" |
| His fee is `132567.52 * Log(level + 1) * level`, plus up to a quarter of itself again, halved - so a level-12 creature costs around two and a half million gold to find | **decompiled source**, FRMSEEK.BAS:94-95 and 242-243 |
| He cannot reach below level 12, and says so | **decompiled source**, FRMSEEK.BAS:90 and 237 |
| He matches the name you type against the start of a name, case-insensitively; an exact match wins outright, and two partial matches make him refuse to guess | **decompiled source**, FRMSEEK.BAS:61-80 |
| The search itself fails `level * 5` times in a hundred even after you have paid, and then he either names the level or admits he has nothing | **decompiled source**, FRMSEEK.BAS:104-112 |
| When he finds a creature he gives coordinates, says how common that general type is in the area - from how many classes the area admits - and, three times in four, how often the creature turns up among its own kind | **decompiled source**, FRMSEEK.BAS:160-206 |
| A creature found in its own lair is "almost always seen" there, and he stops looking as soon as he finds one, three times in four | **decompiled source**, FRMSEEK.BAS:127-133 and 188 |
| For an item he names up to three creatures that might carry it, marking "(very strong)" the ones whose treasure list names that exact item rather than its type | **decompiled source**, FRMSEEK.BAS:262-313 |
| He marks the ground five squares around what he found on your automap | **decompiled source**, `sub321C` called at FRMSEEK.BAS:172 |
| The dungeon and game keys are the ones the Commands page lists: `f` Fight, `d` Defend, `c` Cast, `<space>` "Stop fighting, opening, etc.", `t` Take Stairs, `i` Item Info, `g` Give, `p` Pickup Body, `l` Drop Body, `n` Select next Character, `1-4` Select Monster Group, `Shift 0-9` the buffers, `Alt-s` and `Alt-1..4` for the party | **the original's help**, `4k0o8w1.htm`, cross-checked against the key handlers in FRMDUNM.BAS |
| The cursor keys are the movement keys and nothing else walks you: the letters the port had been using for that - `a`, `d`, `s`, `w` - are all commands of their own in the original | **the original's help**, `4k0o8w1.htm` and the FAQ's "you can use the cursor keys to move around just as you can when there are no monsters in a room" |
| Not every encounter is hostile: a good monster met by a good character is friendly outright, and a neutral one is friendly two times in five. The rest come for you | **decompiled source**, MONSMOD.BAS:1708-1719 |
| A hostile group attacks on every tick and rolls for nothing; only a friendly one rolls to turn hostile, and not even that while it has an offer to join standing | **decompiled source**, MULTIMOD.BAS:551-557 |
| One or more of a friendly group may offer to join you: the monster must be neutral or of your own alignment, must not out-rank your active guild, and a draw of 1 to 3000 must come in under `CHA * 10 - (LevelFound - 1) * 10`. What you get is `Int(Rnd * min(groupSize, 4)) + 1` of them | **decompiled source**, MONSMOD.BAS:1943-1962, matching `Chance_of_Monsters_Joining_Formula.txt` |
| An encounter of more than one group never offers at all, because the original clears the offer at the top of each group's turn and only ever reads group 0's | **decompiled source**, the `Foes(0).M4EF9 = False` at MONSMOD.BAS:1943 inside the group loop |
| "1 Goblin offers to 'j'oin." / "2 Goblins offer to 'j'oin.", and `j` takes them up on it | **decompiled source**, MONSMOD.BAS:4336-4341 and the accept path at MAINMOD2.BAS:4090-4116 |
| Each one that joins takes a companion slot and leaves the room; with every slot full he says "Join - No room for more Companions!" and the offer stands | **decompiled source**, MAINMOD2.BAS:4094-4116 |
| A monster that came of its own free will is bound at -10, the mark for one that cannot be re-bound and will not turn on you when it leaves | **decompiled source**, `fn1163(gv1A86, Foes(0), Foes(0).Hits, -10)` at MAINMOD2.BAS:4099; the help says as much |
| A body in the dungeon stays where it fell. The morgue is only on offer once it is out, so the ways home are to lie there until found, to be raised where you lie by another character, or to have rescuers fetched by a living character at the morgue counter | **decompiled source**, `sub0930` MAINMOD2.BAS:3045-3048 captions the button "Wait for Rescue" while DungeonLevel is above 0 and "Visit the Morgue" only when it is 0 |
| Waiting draws a day at a time: `Rnd * ceiling <= 16 - level` (floored at a tenth), the ceiling being 10,000 until you have lain there longer than `Log(level)² * 100 + 21` days and 1,000 after that. Dying on level 1 in an area of 18 or lower drops the ceiling to 100 | **decompiled source**, `cmdWait_Click` FRMDUNM.BAS:150-176 |
| You are always found in the end - the loop has no other exit - and every day of it is added to your age. A body on level 15 waits about four years | **decompiled source**, FRMDUNM.BAS:177-186, `sub0EAE False, char, days, True` |
| Square flag bit 23 doubles the daily chance of being found. No square in the shipped dungeon carries it, so the bonus never applies; the port reproduces the test anyway | **decompiled source**, FRMDUNM.BAS:153-156; counted across all fifteen levels of MDATA11, the bit is set nowhere |
| A character pulled into the rock gets no wait at all - they have to be summoned out before anything can reach them | **decompiled source**, MAINMOD2.BAS:3045-3050 hides the button for that cause of death |
| The morgue will send rescuers for `372567.52 * Log(level + 1) * level` gold, to coordinates you supply - they must be within eight squares each way and on the exact level | **decompiled source**, FRMMORG.BAS:141-149 |
| Whether the rescuers come back at all is `Rnd * 100 > level * 4`, so they return about 96% of the time from level 1 and about 40% from level 15, the deepest the dungeon goes. The morgue refuses coordinates below level 15 outright | **decompiled source**, FRMMORG.BAS:148 and the `mc008E = 15` bound at 137; "Rescuers never returned...  Don't you feel guilty now?" at 174 |
| What they report: a dead character is brought to the morgue; a living one only if they are down to a fifth of their hits, and otherwise left where they are; one in the rock cannot be fetched - "The only thing they saw were feet sticking out of rock." | **decompiled source**, FRMMORG.BAS:150-171 |
| A chest is not opened for you: it sits there until you press `o` **or click it**, in a fight or out of one | **the original's help**, "To open a chest during peacetime or combat, either press 'o' or click on the chest"; the panel is `monShowChest`, MONSMOD.BAS:4124. The click had nowhere to land until the chest was a picture rather than a paragraph |
| The Library is one list and a pair of radio buttons - creatures or items - with the creature's picture on show only for creatures, and "There are N Monster Entries." above the list | **decompiled source**, `sub327E` (FRMLIBRA.BAS:57-88) and `lstList_Click` (46-55). The names go in in the data's own order with nothing appended; an empty list holds `<NO ENTRIES>` and is disabled. The port had shown both lists side by side, sorted, with "(level 7)" written after each creature, no count and no picture |
| The Exchange screen works by dragging: an item label onto another item label swaps the two slots, and one gold label onto the other asks "How Much Gold?" under the heading "Transfer Gold" | **decompiled source**, `pnlItem_DragDrop` and `pnlGold_DragDrop` (FRMEXCHA.BAS:92-128, 156-214). The gold goes to the panel that was dropped on, and comes out of the giver's purse first and their bank account after. The port had a row of Swap and Give and Gold buttons instead, which is not what the screen's own instructions describe |
| It refuses to open at all if either character owes the bank: "One or both characters still owe money to the Bank and cannot Exchange Items or Gold until it is paid!" | **decompiled source**, FRMMENU.BAS:73-77, checked before frmExchange is shown |
| The two names are shown in double quotes and the gold as "Gold: N" - one number, which is what they carry, plus what they have banked when they are in town | **decompiled source**, FRMEXCHA.BAS:52-53 and `sub2D61` (76-89) |
| The Exchange is reached from the main menu, as the original reaches it: a second character is picked from the roster and loaded off disk, so any two characters can exchange, refused when either owes the bank or when they are not in the same place ("Characters are not at the same location!"). **Corrected** - the port used to reach it from the city, between two party members | **decompiled source**, `cmdCommand_Click` FRMMENU.BAS:68-87 with `sub2D04` (204-243); `TitleWindow.OnExchange` |
| What opening it turned out to be is three words in lblTrap - "Not Trapped", "Trap Disarmed", or the trap's own name - held for half a second and then taken away | **decompiled source**, `monOpenChest` sets `l0510` at MONSMOD.BAS:3503/3507/3510 and writes it at 3650, with `sub07E2 .5#` between that and `picTrap.Visible = False` |
| The trap named on the panel is a guess. Your wisdom plus a quarter of what gear adds, less `Log(monster guild level) * Log(monster DEX) * 2.5`, plus your thieving ability over 1.2 and the square of the log of your thieving guild's level; beat `Int(Rnd * 100) + 1` and you are shown the real trap, fail and you are shown one of the eleven at random | **decompiled source**, MONSMOD.BAS:4129-4146 |
| The diamonds after the name are how sure you are, `Int((confidence / 10) / 2.3)` clamped 1..4 - and they come from the same figure as the roll, so the panel never tells you whether it guessed right | **decompiled source**, MONSMOD.BAS:4150-4153 |
| The `+`, `*` or `-` before the diamonds is a separate question - your chance of *disarming* it: plus above 55, star from 45 to 55, minus below, one to three of them | **decompiled source**, MONSMOD.BAS:4154-4164 |
| A magically locked chest shows "Magically Locked" and nothing else, and `o` does nothing at all until a Charm of Opening unlocks it. The test is `lock state <= gv1D7E`, and `gv1D7E` is assigned nowhere in the program, so it is 0 | **decompiled source**, MONSMOD.BAS:4128 and 4173; the help says "If the chest is Magically Locked or Unidentified, nothing will happen" |
| "Going through Treasure..." while it is opened | **decompiled source**, MONSMOD.BAS:3670 |
| Two figures the chest panel computes and throws away - `l0604` and the first `l0636` - are dead, the second being overwritten on the line after it is built | **decompiled source**, MONSMOD.BAS:4134-4136 |
| A chest does not hand over one item and stop. Having given you something it comes back round at `Int(ItemChance / 10)` and may give another, and another - a monster carrying 99% averages 1.1 items and sometimes gives three | **decompiled source**, the `Do ... Loop` wrapping MONSMOD.BAS:3669-3861, with the repeat threshold set at 3859 |
| Forcing a magic lock costs you: a chest opened by a Charm of Opening has `Log(level + 10) * 5` added to its drop roll, making an item *less* likely. The community page has this the wrong way round | **decompiled source**, MONSMOD.BAS:3665-3667, testing for `gc1D7C` - the -1 that only a spell-opened chest carries |
| The item pool is a **contiguous run of the item table**, not a filter: the first record of the drawn type at or above the floor, running forward until the type changes or the floor passes the top, weighted by rarity across that stretch. Items sitting past a break in the run cannot drop at all | **decompiled source**, MONSMOD.BAS:3723-3773, and `Item_Generation_Formula.txt` describes the same thing |
| One of the monster's ten treasure entries is drawn each time. -1 means any item type at all; anything below -1 names one exact item, stored as `-(id + 2)` | **decompiled source**, MONSMOD.BAS:3703-3722 |
| Landing on an item whose floor is exactly the top of the band widens the band downwards and draws again; finding nothing of that type in reach restarts the whole generation, band and all | **decompiled source**, MONSMOD.BAS:3730-3741 |
| Each item goes to a party member picked at random, retried up to ten times to find one with a free slot; if none has room, "An item was found, but nobody had room to carry it." | **decompiled source**, MONSMOD.BAS:3778-3799 |
| A cursed item puts itself on as it arrives: "An item you found has attached itself to you!!" | **decompiled source**, MONSMOD.BAS:3850-3858 |
| The game does not tell you what square you are standing on. Perception does, or fails to: `Int(Rnd * 100) + 1 > your perception` and you notice nothing at all | **decompiled source**, `sub087C` MAZEMOD.BAS:614-625; the help: "Characters with a high perception ability are usually able to detect a difference in the maze when walking around" |
| Only the square types you cannot see are ever spoken about - the four facing squares, the extinguisher, the rotator, anti-magic and the stud. A pit or water announces itself by what it does to you | **decompiled source**, the eight `sub087C` call sites at MAZEMOD.BAS:1703-1726 and 1785 |
| Noticing something and working out what it is are two rolls. The second is against *half* your perception, and only that one puts the square on your automap | **decompiled source**, MAZEMOD.BAS:630-646. The port kept that answer in a notebook of its own and never wrote it to the map, so the six things perception exists for - rotator, teleporter, anti-magic, extinguisher, stud, rock - could be identified and still never appear. `fn0ABD` at MAZEMOD.BAS:641 writes the bit into the automap file itself, which is one store, not two |
| **The party perceives the square ahead as well as the one it is on.** Every step and every turn, the best perception in the party rolls against each of eight things in the square you are facing - quicksand, both staircases, a teleporter, a chute, a pit, a rotator, an extinguish square - and says so: "You detect a Pit ahead of you." alone, or "Tester" detects a Pit ahead of the party." in company | **decompiled source**, `mdrmazPerceiveAhead` (MAZEMOD.BAS:450-600), run at the end of `mdrmazShowMaze` (1487). The port did not have it at all |
| The square you are standing on announces itself in one line at a time: "There are Stairs Up here.", "There are Stairs Down here.", "There is a chute here.  Select TAKE to use.", "You are hovering over a pit.", "You have fallen into a pit for 12 hits." | **decompiled source**, `lblSquareInfo` - MAZEMOD.BAS:1545 blanks it each square and 1766, 1774, 1780, 1783 and 1863 overwrite each other in that order, so a square with both staircases reads about the down ones. The port had built a list of its own instead: "You see stairs up, water." |
| A guild does not list what you are missing - it speaks: "We don't accept Elfs!", "You're Evil!  Go away!", "Your natural stats are too low to join!", or "Welcome, Visitor." More than one refusal can be said at once, in that order | **decompiled source**, `sub2962` (FRMCTYG.BAS:782-812). A guild you already belong to says "Welcome, Brother." or "Welcome, Sister." and its button reads Re-Acquaint |
| A won fight is not announced. What is said is the gold: "Your share is 129 Gold!", or "Your share is 129 and you recovered 40 Gold!" when you killed something that had robbed you | **decompiled source**, `monGetGold` (MONSMOD.BAS:2144-2178). The port had written "Victory! You gain 27 experience and 173 gold.", which is neither the wording nor the shape - the experience is only mentioned when it takes somebody past a level |
| You cannot leave the dungeon while anything is standing on your square, engaged or not: "Cannot exit while monsters are in the area!" | **decompiled source**, `Form_QueryUnload` (FRMMAIN.BAS:186-190), which beeps and cancels. The test is `fn0EEF()` - monsters present - not whether a fight has started, so a peaced group stops you too |
| Entering an area (or the dungeon) tells you who else is in it: "There is a dead body here!" or "There are dead bodies here!", and "Somebody else is in the room!" or "Other characters are in the room!" | **decompiled source**, `sub09CC` (MAZEMOD.BAS:348-380) over `mdrmazLookAround`, which walks the game's whole roster and not just your party - somebody else's character, left dead in the dungeon, is a body in your room. It matches by floor and **area** (MAZEMOD.BAS:286-287), and runs only at FRMDUNM.BAS:282 (the dungeon form's load) and MAZEMOD.BAS:844 (`mdrmazShowMaze` when the area or level changed). The port had neither line; then it had them by exact square on every step (corrected 2026-09-27, below) |
| A body somebody in the party is already carrying is not a body lying here | **decompiled source**, MAZEMOD.BAS:355-358: each member whose `M5943` is four or more - the range that means "carrying a character" rather than a companion - takes one off the count |
| Your own corpse is not a body you notice | **decompiled source**, MAZEMOD.BAS:288, which excludes `Chars(m00C0)` from the dead tally |
| When the name box has nobody to offer it says so **in the box**, greyed, with OK disabled - and which sentence depends on what was asked: "No other characters in the area!", "No characters in the party!", "No characters to resurrect!", or "No characters available for this selection!" | **decompiled source**, `frmInteract` (FRMINTER.BAS:435-453). The port had a line of its own for each of these places |
| A raise spell is refused outright while monsters are on the square: "Cannot raise character in Combat!" | **decompiled source**, MAINMOD2.BAS:3479-3482, testing `fn0EEF()` |
| A heal asks who it is for: "Cast on Who?", over the party and the caster's companions | **decompiled source**, MAINMOD2.BAS:933 (`fn170D(..., "Cast on Who?", "", "SPELLHEAL")`) and FRMINTER.BAS:397-411, which shows a button per companion. Cure Poison and Cure Disease go through the same box under the tag "SPELL" and offer the party alone. The port had healed the caster and nobody else |
| Reloading a saved party does not simply take everybody back. Each remembered member is loaded and tested three times, and every outcome is reported by name under "AUTOLOAD PARTY: " - "not here anymore!", "is dead!  Can't join!", "is oppositely aligned to one or more members of the party!", or "is now a member of your party." | **decompiled source**, `mdrmulLoadParty` (MULTIMOD.BAS:240-270). The alignment test is `Abs(difference) > 1`, so Good and Evil will not travel together and Neutral travels with either. A character whose file will not load gets a question instead: "An error occurred auto-loading \"Sela\".  Has the character been deleted?" The port had taken everybody back in silence |
| A teleporter can kill the whole party two ways. A destination above the city - level zero - means "You have teleported hundreds of feet above the city and fallen to your death!" in a box headed "Teleport", and everybody dies. A destination in rock, or below the deepest level there is, means "You teleported into solid rock!" - the square is written onto your automap as rock, and everybody dies the death only Resurrect undoes | **decompiled source**, `mdrcharTeleport` (MAINMOD2.BAS:5839-5893). The port had assumed a teleporter always put you somewhere survivable |
| The Teleport and Displacement **spells** go through the same routine and can end the same two ways. Asking to go up from level one puts you above the city, and you fall | **decompiled source**, MAINMOD2.BAS:1125 and 1143 both call `mdrcharTeleport`. The port had clamped the level at zero and stayed put, and had written its own two sentences for the rock death - "Tester materialises inside solid rock!" and "You walked into solid rock!" |
| "You are teleported!" is not said. A teleporter announces itself through perception - "You just hit a teleporter!", which it always gives you whatever your perception | **decompiled source**, MAZEMOD.BAS:659-665. The port said both, so the arrival was announced twice |
| Dying of old age says "You have died of natural causes.", or "Sela" has died of natural causes. - the whole sentence changes | **decompiled source**, FRMDUNM.BAS:571-575. The port had stuck the name on the front of one sentence |
| Every death reason is a `sub085B`, which is the Messages window - a list that keeps what it is told - not a caption that the next event wipes | **decompiled source**: FRMDUNM.BAS:572 for old age, MONSMOD.BAS and MAINMOD2.BAS for the rest all call `sub085B`. The port had been writing them to the message line only, where the rescue offer that follows a death outside combat overwrote them immediately, so a body was left on screen with no account of how it got there |
| So is every refusal. "Cannot exit while monsters are in the area!", "Leader cannot leave Party!", the two Party Options wordings, the two give-during-combat lines and "X cannot leave while monsters are in the area!" are all `sub085B` calls, so they belong in the Messages window and not only on the port's own message line | **decompiled source**: FRMMAIN.BAS:188, MULTIMOD.BAS:745 and 750, FRMGROUP.BAS:41 and 197, MAINMOD2.BAS:4059. The original has no message line at the foot of the screen at all - that is the port's addition, and anything written only there is gone as soon as the next event happens |
| Equipped gear you can no longer use comes off by itself. Joining a guild does it quietly and says one line for the lot - "GUILD: One or more items were unequipped when you joined..." - while a stat drain in a fight names each item as it goes | **decompiled source**, `mdrcharCheckItems` (MAINMOD2.BAS:667-689), called from FRMCTYG.BAS:159 with the announcement off and MONSMOD.BAS:3190 with it on. The port had left forbidden gear equipped |
| The stat drain runs the same check with the announcement on, so each item names itself as it comes off | **decompiled source**, MONSMOD.BAS:3190, immediately after "You have been drained!" |
| The companion buttons are shown for a companion whether it is alive or dead; picking a dead one answers "Companion is Dead!" and nothing is cast | **decompiled source**, FRMINTER.BAS:402-406 (only the **raise** box hides the living ones) and MAINMOD2.BAS:940-943. The refusal had nowhere to land until the box existed |
| A heal that works says nothing at all | **decompiled source**, MAINMOD2.BAS:951-982 - it heals, caps at the maximum and redraws. The port had written "You cast Minor Heal and recover 10 hits. (97 sp left)", refused at full health - a check the original does not make - and halved the healing on a diseased character, which is not in the formula |
| Spell effect 18 is the heal that also clears poison and disease | **decompiled source**, MAINMOD2.BAS:974-977 |
| You cannot raise the body you are carrying: "Drop the character before you raise them!" The test is that narrow - it compares the name of the one being raised against the name of the one over your shoulder, so carrying somebody else, or a companion, is no obstacle | **decompiled source**, MAINMOD2.BAS:3484-3489. The port had refused whenever your hands were full, in a sentence of its own |
| Asking somebody to leave the party refuses twice before it asks anything: "Tester" cannot leave while monsters are in the area! below ground, and "Leader cannot leave Party!" for whoever stands at position one | **decompiled source**, `sub159D` (MULTIMOD.BAS:739-751). This port had read `sub04DC` instead - the removal itself, which does no checking - and thrown both refusals away as inventions of its own |
| Party Options is refused while monsters are present, peaced ones included | **decompiled source**, FRMGROUP.BAS:38-42 and MAINMOD2.BAS:4057-4061, both testing `fn0EEF()`. The port had been testing whether a fight had started |
| The gold is divided among **everyone**, the dead included, and each of them gets the same `CLng(total / count)` | **decompiled source**, MONSMOD.BAS:2151-2153 - the loop runs `0 To UBound(Chars)` and asks nothing about who is standing. The port had paid only the able and handed the remainder to the first of them |
| Giving refuses three ways: "Cannot give Guild-Issued Items!", "Cannot give equipped items!", and "Brannoc" has no free slots! | **decompiled source**, `sub14B5` (MAINMOD2.BAS:2936-2954). The port had taken an equipped item off first and said "The Elven Ring will not come off." when it was cursed, neither of which is in the routine |
| A handover that works says "Give of Item to \"Brannoc\" Completed!", after "\"Brannoc\" identified the given item as: ..." when the person receiving it can read the item better than its owner could; gold says "Give of 500 gold to \"Brannoc\" Completed!" | **decompiled source**, MAINMOD2.BAS:2934, 2956-2962 (the same re-reading the Exchange does) and 3004-3006 |
| Making a level: "You cannot learn any more from this Guild." on the message line at the cap, then "You do not currently have enough experience to make a level." and "You currently do not have enough gold to make a level." in boxes - each of which opens the Info box behind it - and "Congratulations! You are now Level 5!" when it works | **decompiled source**, `cmdMake_Click` (FRMCTYG.BAS:175-310) and 417. Every one of these was a sentence of the port's own |
| The Info box: what the guild wants and what you have, four lines, with the quest under them | **decompiled source**, `cmdInfo_Click` (FRMCTYG.BAS:45-81). The port had no such box |
| Joining asks "Cost to join the Ninja's guild / is 5,000. / Do you wish to join?", or just "Join the Ninja's guild?" when it is free, under a warning if your best guild level is below ten | **decompiled source**, FRMCTYG.BAS:110-128 |
| Water and quicksand say nothing as you step in. They speak on the ailment clock, once a tick, for as long as you stand there: "You are Drowning!" and "You are Sinking!" | **decompiled source**, FRMDUNM.BAS:504-511, which writes them into the same `lblSquareInfo`. The port said "You wade through cold water." and "You struggle through quicksand." on the step - its own words, at a moment the original is silent |
| A character is named in a list as "Tester (Level 5 Ninja)", with the state of the body in capitals after it if there is one | **decompiled source**, FRMCHARL.BAS:51-53 and `gv0814$` (MODULE1.BAS:449-457): Dead, Stoned, Destroyed, Immolated, Frozen, Annihalated, Melted, Rock. "Annihalated" is spelt that way in the game |
| The Party Options buffer box lists 1 to 10 | **decompiled source**, FRMPINFO.BAS:35-37. The port had listed 1..9 then 0, on the grounds that Shift+0 fires the tenth - true of the key, not of this list |
| **A spell is a COMBAT spell or a NON-COMBAT spell, and each can only be thrown at the right time.** "COMBAT spells can only be cast in combat while NON-COMBAT spells can only be cast when there are no monsters, or there are peaced monsters in the area" | **the original's help** (Spells), which labels every spell page one or the other, and **decompiled source**: `fn149E` (MAINMOD2.BAS:2908) computes it from MDATA2 rather than storing it - non-combat when `Damage2 = 0` or the school is 9 (Heal), combat otherwise, and "either" when the effect is Teleport or Cure Paralysis. The help has a third label too - Teleport's page reads "COMBAT & NON-COMBAT Spell" - and it falls exactly where the routine says "either". The two sources agree on all 91 spells the help labels and this port has, and disagree on none; `selfcheck` asserts both the nil disagreement and the count, so a matcher that quietly stopped finding labels cannot pass it by comparing nothing. The one spell they are not identical on is Cure Paralysis, where `fn149E` says either and the help says COMBAT - the help saying less than the routine rather than contradicting it. See `Mordor.DataDump castgate`. The port had no such rule |
| **Healing is a non-combat spell.** Once the monsters have engaged you cannot heal | the same two sources - school 9 is Heal, and every heal page in the help reads "NON-COMBAT Spell, will heal hits" |
| Casting on an anti-magic **or an extinguish** square says "Anti-Magic area!", spends nothing, and forces the anti-magic bit onto your automap | **decompiled source**, MAINMOD2.BAS:520-525. The port let the cast begin, took the spell points, said "You begin X, but the anti-magic field swallows it." - a line of its own - and did not check the extinguish square at all |
| A combat spell above level 7 cannot be thrown at monsters who have not engaged you: "X Takes too long to throw when peaced, Name." | **decompiled source**, MAINMOD2.BAS:536-540; at or below level 7 the cast goes through and starts the fight |
| Running out of spell points says "Not enough spell points to cast \"X\"!", or names the character when it is not the one you are looking at | **decompiled source**, MAINMOD2.BAS:502-508. Every refusal in this routine has two wordings for the same reason |
| It cannot see through a wall, a **door** or a secret door on the side you are facing, nor off the edge of the map | **decompiled source**, MAZEMOD.BAS:479-511 - the four facings each test the edge between here and there before stepping, and a door blocks it exactly as a wall does |
| Everything it finds is written into the automap, which is one of the only two ways a rotator or an extinguish square can ever get an icon there | **decompiled source**, `fn0ABD` at MAZEMOD.BAS:595, followed by `frmAutoMap!lblCall = "x,y"` to redraw that one square |
| The perception used is the best in the party, capped at 95, and the loop counts **every** member - the dead and the stoned included | **decompiled source**, MAZEMOD.BAS:466-475, and the same loop in `mdrmazPerceiveSquare` at 620. The port had been asking only the able |
| Fail the second roll and you are told a square type **at random** - never a pit, staircase, water, quicksand, rock, fog, chute or teleporter, because you would have noticed those yourself - with just as much confidence as if you were right | **decompiled source**, MAZEMOD.BAS:648-656 |
| The certainty ladder, word for word: "kind of" to 39, "fairly" to 49, "strongly" to 59, "almost very" to 69, "very" to 85, "almost absolutely" to 95, "absolutely" above - and "completely" when you have actually mapped it | **decompiled source**, MAZEMOD.BAS:667-684 |
| Below a perception of 35 you get no more than "You sense something different here..."; above it, "You are `<certainty>` certain you're standing on a `<name>` square." | **decompiled source**, MAZEMOD.BAS:687-708 |
| The square names are the game's own: Right Wall, Upper Wall, Right Door, Upper Door, Right SDoor, Upper SDoor, Face North/East/South/West, Extinguish, Pit, Stairs Up, Stairs Down, Teleporter, Water, Quicksand, Rotator, Anti-Magic, Rock, Fog, Chute, Stud | **decompiled source**, `gv194E`, MODULE1.BAS:553-575 |
| A square you have already worked out says nothing a second time | **decompiled source**, MAZEMOD.BAS:627, which exits when the flag is already on your automap |
| An extinguisher square strips every party member's magical protection mask and all eleven protection values | **decompiled source**, MAZEMOD.BAS:1738-1747 |
| Every monster name is read from what the party *believes* the creature is (`M4F01`), not from what it is (`M4ED4`). A party too weak to read something is given another creature's name and keeps being given it until somebody works it out | **decompiled source**, `fn0E23` takes `l03E8 = p03E6.M4F01` at MONSMOD.BAS:2279; the FAQ: "How come some monster's names change while I'm fighting them? Because your character mis-identified the monster and later realized what it really was" |
| Whether you read it right: `50 + (identify skill - Log(guild level) * (floor + 1)) * 10`, clamped 5 to 95 | **decompiled source**, MONSMOD.BAS:1883-1892 |
| The wrong name is another monster of the same **subtype**, stepped up or down that subtype's stretch of the table by `Int(Rnd * (Rnd * (Log(floor) + 2.5) + 1.5))`, and it must stay inside its own class's run. After a hundred failed attempts the original gives up and uses the real one | **decompiled source**, MONSMOD.BAS:1893-1942 |
| Putting the name right is a separate roll from lifting fog, on the same four-second clock: `identify skill + 20`, cut to three quarters *in* fog rather than raised outside it, clamped 10 to 90 | **decompiled source**, FRMDUNM.BAS:475-489, against the fog pass at 456-464 |
| Holding and Confinement buys creatures out of its pens, buys them back, identifies them and realigns them, as well as binding them. The pens start empty and only fill as characters sell into them | **decompiled source**, `cmdBuy_Click` FRMCITYC.BAS:96, `cmdSell_Click` 272, `cmdID_Click` 177, `cmdRealign_Click` 238; MODULE1.BAS:819 loads the pens empty on a fresh file |
| What a creature is worth: 500 a point of attack and defence, 250 a hit, scaled by `Log(STR + DEX)` and `Log(guild level + 1)`, plus a tariff for every special attack and spell school it has - the dearest at 5,250 a guild level - and 500 a point of resistance | **decompiled source**, `fn33A6` FRMCITYC.BAS:189 |
| Each of those surcharges is written `3250@ * GuildLevel + 1`, which multiplies before it adds, so the trailing `+ 1` is a single gold piece rather than a bump to the level. Reproduced as written | **decompiled source**, FRMCITYC.BAS:196-212 |
| Buying costs `1.5 * value / (Log(stock - 1 + 1) + 1.15)`; selling pays `0.9 * value / (stock + 1.15) / (how well you know it + 1)`, so an unidentified companion fetches a fraction | **decompiled source**, `fn33CC` FRMCITYC.BAS:213 and `fn33FA` 246 |
| Realigning costs `0.72 * value`, doubled if you ask for the far alignment rather than a neighbouring one | **decompiled source**, `cmdRealign_Click` FRMCITYC.BAS:248 |
| A bought companion arrives bound at `5 + its floor`, capped at 10 | **decompiled source**, FRMCITYC.BAS:134-138 |
| A companion that joined of its own free will cannot be bound at any price: "You cannot bind a companion that has joined you free of will." | **decompiled source**, FRMCITYC.BAS:766 |
| The Bank holds forty items as well as gold, it will not take anything you are wearing, and it says "You have N gold and M free Bank slots available." | **decompiled source**, `lstItems_DragDrop` FRMCITYB.BAS:168-199 and `sub2884` 274-284 |
| With a party the Bank offers Pool & Deposit, Party Deposit and Party Pool & Deposit, and greys them out when you are alone | **decompiled source**, FRMCITYB.BAS:34-54, disabled at 283-285 when the party is one |
| A hurt party member who is not the one you are watching calls out: "Aigh!" at zero hits, "Ouch!  Hit for N!" from a third of their hits up, and "Help! My hits are N/M!" below ten or a tenth | **decompiled source**, `sub0CA0` MAINMOD2.BAS:5666 and MONSMOD.BAS:3400 |
| Paralysis wears off on the four-second ailment clock, not on the combat round, and always once nothing is left on the square | **decompiled source**, FRMDUNM.BAS:550-560 |
| A wiped-out group leaves no row behind: the slot is simply hidden, and the groups that remain keep their own numbers. The count is printed only for monsters that come in groups of 2 to 12 | **decompiled source**, MODULE4.BAS:4404-4412 |
| A group's identification level is held per slot, not per creature kind, so two separate groups of the same monster are worked out separately | **decompiled source**, MDATA5 `Foes(0..3).M4EEE`, FRMDUNM.BAS:455-497 |
| **An item of the wrong alignment is not merely useless.** Match or unaligned gives full value; one step away (Good on a Neutral, Neutral on an Evil) gives *half*; two steps away (Good on an Evil) gives the value **negated** and curses the item on | **decompiled source**, `sub0F74`, MAINMOD2.BAS:1817-1828 for attack and defence, 1846-1853 for stat modifiers. The port had the middle case as "no benefit" |
| The two halvings are written differently and behave differently. Attack and defence use `Int(x) / 2`, which lands in an `Integer` and so rounds half to even: 5 gives 2, 7 gives 4. Stat modifiers use `Int(x / 2)`, which floors: 5 gives 2, 7 gives 3 | **decompiled source**, MAINMOD2.BAS:1822-1823 against 1849 |
| Effect bits and resistances are all-or-nothing: `sub120F` counts an item only when its alignment is unaligned or an exact match, so a half-worth item gives none of its effects | **decompiled source**, MAINMOD2.BAS:1866-1895 (`M4EBF = 0 Or M4EBF - 1 = char.M4EBF`) |
| **Two hands, no more.** Equipping checks `character.hands + item.hands > 2` and says "No Free Hands!" - so a two-handed sword and a shield will not go on together, in either order | **decompiled source**, `fn11B6`, MAINMOD2.BAS:572-575 |
| **You may wear only so many of a kind.** Each item type points at a class (Weapon, Armor, Shield, Head Protection, ...) and each class carries a maximum; reaching it gives "Using too Many!". Sixteen classes allow exactly one, so one weapon and one suit of armour is the whole of it | **decompiled source**, `fn11B6`, MAINMOD2.BAS:576-583, reading `gv1592(type).M54D5` then `gv15C6(class).M54F1`; the values are in MDATA1 |
| Both of those are checked *before* the guild, the guild level and the stats, and both are skipped when the caller passes a non-zero fourth argument - which the store, the guild hall and the character editor all do, because they only want to know whether the character could ever use the thing | **decompiled source**, MAINMOD2.BAS:572 and 576 (`And p0160 = False`), against the callers at FRMCTYG.BAS:933, FRMCTYS.BAS:1013, FRMEDITC.BAS:241 and MAINMOD2.BAS:1776 |
| A class maximum of zero means the item is not worn at all: the equip toggle looks it up first and returns without a word, which is why a potion, a scroll or a tome cannot be equipped and you are told nothing | **decompiled source**, `sub0F74`, MAINMOD2.BAS:1724-1726 |
| Equipping must not push a stat below your **race's** minimum - a check `fn11B6` never makes. The modifier counted is the one the item is actually worth to you, so a half-worth item is half as likely to be refused. A cursed item gets its own wording | **decompiled source**, MAINMOD2.BAS:1797-1815, comparing against `gv148E(race).M5530`; messages "You are too weak to equip this item!" and, for `item.Cursed = 2`, "Your stats are too low for the item to properly attach!" |
| **Use is not one command.** An item can cast the spell it carries, permanently change a stat, hand over spell points, or take years off your age - and an item with both a spell and a special type does the spell first, then the rest | **decompiled source**, `sub15CD`, MAINMOD2.BAS:6081-6240; the spell at 6112-6143, the rest from 6144 |
| Only six special types exist in MDATA3: 1 applies the item's stat modifiers (15 potions), 11 gives 50 spell points (Scroll of Spells), 12 gives 200 (Tome of Spells), 13 takes off a year (Potion of Youth), 14 takes off ten (Dragon's Blood), and 500-511 are the twelve guild crests. 334 items are type 0 and do nothing beyond their spell | **decompiled source** for the branches, MAINMOD2.BAS:6156-6200; **data file** MDATA3 for which values occur |
| Using anything that is not a guild crest plays MAGIC.WAV | **decompiled source**, `sub086D mc0864` with `mc0864 = 4`, and the sound table at MODULE1.BAS:304-345 where index 4 is MAGIC.WAV |
| A stat potion's ceiling is your race's maximum **plus five** - which is how a character gets past what they were born able to reach - and hitting the ceiling is silent, because the announcement only lives in the branch where the value did not clamp | **decompiled source**, MAINMOD2.BAS:6207-6208, 6214-6218, against `gv148E(race).M553C` |
| An item only casts its spell for its own alignment: unaligned or an exact match works, and anything else is "Nothing Happens." There is no half here | **decompiled source**, MAINMOD2.BAS:6113-6117 |
| The non-spell effects use a different rule again: one step away is "Nothing Happens.", two steps away **reverses** the item, subtracting the modifiers it would have added. Only positive modifiers are reversed - a negative one still hurts you | **decompiled source**, MAINMOD2.BAS:6147-6153 for the test; the reversal is 6205-6211, whose first line (`l085C = True And M571E > False`) is the positive-only guard |
| The charge is spent before either of those is decided, so a one-step item costs you a charge and does nothing at all | **decompiled source**, `mdrcharSubtractItemUse` at MAINMOD2.BAS:6145 runs before the alignment test at 6147-6152 |
| A weapon, a suit of armour, an amulet, a stone, an artifact, a miscellaneous item or a guild crest must be **worn** before Use will do anything; a potion, scroll, tome, dust, crystal, rod, sphere or cube works from the pack | **decompiled source**, MAINMOD2.BAS:6119-6124 (`Case 1 To 22, 29, 32, 33, 34`) |
| An item with no charges at the data level says "the Item doesn't appear to do much of anything"; one whose own charges have run out says "the Item is out of charges!" - two different messages for two different states, both naming the character | **decompiled source**, MAINMOD2.BAS:6098-6104 |
| On its last charge a Potion, Scroll, Tome, Dust, Crystal, Rod, Sphere or Cube is destroyed; anything else stays in the pack at zero charges, which is how a spent guild crest is still your crest | **decompiled source**, `mdrcharSubtractItemUse`, MAINMOD2.BAS:5695-5701. The port had named only four of the eight |
| Use is refused outside the dungeon: "Must be in Dungeon to Use items." | **decompiled source**, FRMCHAR3.BAS:207-211 |
| **Drop destroys the item.** Nothing is left on the floor to pick up again | **decompiled source**, `sub0EDA` blanks the slot outright, MAINMOD2.BAS:5697-5712 |
| A guild crest cannot be dropped ("Cannot drop Guild-Issued Items!") or given ("Cannot give Guild-Issued Items!"), and an equipped cursed item cannot be dropped - but the original asks for confirmation *first* and only then tells you it is cursed | **decompiled source**, FRMCHAR3.BAS:216-236, with `fn11CA` at MAINMOD2.BAS:652 testing `SpecialType` against 500..500+guilds-1 |
| A cursed item you are **not** wearing drops perfectly well: the refusal tests the curse and the equipped flag together | **decompiled source**, FRMCHAR3.BAS:231 |
| **The pack holds thirty-nine things, not forty**, and Buy looks for a free slot *before* it looks at your purse: "No free slots!" | **decompiled source**, FRMCTYS.BAS:50-59, scanning `For l006E = 1 To mc0078 - 1` with `mc0078 = 40` |
| Buying only takes one off the shelf when the item comes from the dungeon, or when you bought an *aligned* copy. The store's own unaligned floor-0 stock is bottomless - it is seeded at 99 and never moves | **decompiled source**, FRMCTYS.BAS:72 (`If gv1426(l0072).M5693 > False Or m0028% > 0`), with the seed at MAINMOD.BAS:897 |
| Selling puts it back on the shelf unless it is an unaligned floor-0 item, and the test is the **floor alone** - not the price, not the item type | **decompiled source**, FRMCTYS.BAS:258 |
| A cursed item is never shelved, and no alignment of one item goes above nine | **decompiled source**, FRMCTYS.BAS:261, with `mc00B6 = 9` |
| **A guild crest cannot be sold**: the panel refuses before it will even quote a price | **decompiled source**, FRMCTYS.BAS:833-837, `fn11CA` again |
| Selling something you are wearing takes it off for you and sells it. Only equipped *and* cursed refuses, with "Item is Equipped and Cursed!" | **decompiled source**, FRMCTYS.BAS:228-236 |
| Uncurse asks three things in order: is it cursed, is it equipped, can you pay. It will not lift a curse on something in your pack - the curse flag lives on the slot and is only set when you put the thing on | **decompiled source**, FRMCTYS.BAS:292-302 |
| Uncursing completely identifies the item as well, and costs 1.6 times its price | **decompiled source**, FRMCTYS.BAS:305-306, and `fn274F = (.8# * price) / .5#` |
| It only clears the curse for good when the *item* is not inherently cursed. An alignment curse is lifted; a cursed sword sticks again the moment you put it back on | **decompiled source**, FRMCTYS.BAS:309-311 |
| **Combine** costs nothing and only works on the eight charge-carrying types. Every item in the list must be completely identified, or the whole operation is refused; then each one must match the first in type, in item, and in alignment | **decompiled source**, `sub2621`, FRMCTYS.BAS:643-690 |
| The shelf refills - and empties - while you rest: each pass has a `Clamp(days, 1, 50)` percent chance of running again, and each pass is **60% someone selling in, 40% someone buying out** | **decompiled source**, `mdrReplenishStore` (`sub071B`), MAINMOD.BAS:1506-1513 |
| **The Confinement ships with ten creatures penned, two of each**: the first two Humanoids, Animals, Insects and Thieves, plus the first Clean-Up and the first Undead on their own - Kobold, Orc, Clean-Up, Black Bear, Giant Owl, Giant Spider, Giant Centipede, Zombie, Footpad and Gredlan Rogue, each at its own alignment. The port used to start with the pens empty | **decompiled source**, `sub054E`, MAINMOD.BAS:522-559, naming them by table index, `gv1DD2(kind, 0)` and the index after it (`Confinement.Fresh`, keyed by index since 2026-10-18) |
| Each pass picks a floor biased shallow, then a random item **type** (rounded into an Integer, so type 0 comes up half as often as the rest, and as often again the draw names no type and goes round), then one item of that type weighted by Rarity | **decompiled source**, MAINMOD.BAS:1518-1572 |
| **Corrected 2026-10-18** - the rows above were marked faithful, but three details were not. (1) **A full shelf wastes the pass**: the item is drawn and its alignment rolled, `If Shelf(l02C4).Align(l02C2) < mc0298` adds only below nine, and `Loop While l02C4 = False` ends the loop either way; the port drew another item, up to 100 times. (2) **The Rarity weights are whole percents**: `l02C8()` is an Integer array, so each `(Rarity / total) * 100` (a Single) rounds half to even; an item under half a percent of its run weighs nothing, and when the weights sum short of the `Rnd * 100` nothing is picked and the loop goes round (7 kinds at floor 3 sum short). The port used exact doubles. (3) **The run and the caps**: the run starts at the first item of the kind within the floor (an index of 0 counts as not found) and ends one before the first deeper item or other kind; a run that reaches the end of the file leaves `l02B6` as it last stood, in this pass or an earlier one, before the clamp to the start. The passes and the selling-in loop have no cap and only the buying-out loop gives up, after 100; the port capped the passes at 500 and both loops at 100. The alignment of an item sold in is `Int(Rnd * 4)` drawn until its bit is set, one draw per try, where the port drew once from a list. Not ported here: each item sold in is filed in the Library (MAINMOD.BAS:1586) | **decompiled source**, `mdrReplenishStore` MAINMOD.BAS:1501, 1537-1553, 1558-1572, 1576-1593, 1671; `StoreStock.Replenish`; selfcheck "the Store restock (mdrReplenishStore, MAINMOD.BAS:1484-1683)" |
| The pens refill and empty while you rest, on the same aging that restocks the General Store, with the same shape: 60% penned in, 40% bought out, a floor biased shallow, a random creature kind, then one creature of that kind weighted by its Chance | **decompiled source**, `mdrReplenishCStore` (`sub0709`), MAINMOD.BAS:1284-1477 |
| A creature from the town level is never penned by anyone else | **decompiled source**, MAINMOD.BAS:1371 (`If gv14F6(l028A).M5693 > False`) |
| **The dead leave the party where they fell.** Anyone at or below zero hits is dropped from the roster, their body left on its square and their character saved so a rescuer can still find it | **decompiled source**, `sub2F40`, FRMGROUP.BAS:321-348, with `sub04DC` (MAINMOD.BAS:414) doing the saving and the shifting |
| It only prunes while more than one person is in the party (`UBound(gv14C2) > m0052`), so a character adventuring alone who dies stays put - which is what leaves the Wait for Rescue screen a body to talk about | **decompiled source**, FRMGROUP.BAS:323 |
| The death is announced only when they died on the **leader's** level. Someone who fell two floors up simply vanishes from the roster without a word | **decompiled source**, FRMGROUP.BAS:326 (`If gv14C2(l0118).M5470 = gv14C2(m0052).M5470`) |
| **The party leader may leave.** There is no exemption anywhere: the leader is only ever whoever stands at position one, and `sub04DC` removes whichever index it is given and shifts the rest down | **decompiled source**, `sub04DC`, MAINMOD.BAS:424-431. The port refused with "The leader cannot leave their own party", which is an invented rule |
| Dragging one name onto another in the party window **swaps** them, taking each one's standing order along, and prints "X is now party leader!" when one end of the swap was position one | **decompiled source**, `lblName_DragDrop`, FRMGROUP.BAS:218-239. The port lifted one out and re-inserted it, which rotates everyone in between |
| The Combat Setting has four values, not three: None, Fight, Defend and **Spell**, where Spell names one of the ten buffers | **decompiled source**, `gv058A$` at MODULE1.BAS:416-419, and the help: "None, Fight, Defend, or a spell buffer can be selected" |
| Everyone's standing order is cleared the moment no monsters are left; the Combat Setting is not, because it is a setting rather than a state | **decompiled source**, FRMGROUP.BAS:413-418, guarded by `fn0EEF() = False` |
| **The automap is drawn from what has been recorded, never from the maze.** The original keeps a flag word per square per level in its own file, and every cell the plot routine puts on screen comes from that | **decompiled source**, `fn2EB5` (FRMAUTOM.BAS:520) and `medPlotMaze` (FRMAUTOM.BAS:390-440) |
| Only some flags are ever written down: the square's own east and north walls and doors, and seven features - pit, stairs up, stairs down, water, quicksand, fog and chute | **decompiled source**, MAZEMOD.BAS:1583 and 1609 |
| A teleporter, rotator, anti-magic square, rock, stud, extinguisher or facing square is **never** recorded, however often you cross it. That is what Perception is for | **decompiled source**, the same `Select Case` - those bits have no branch |
| **A secret door is written down as a plain wall** until you notice it, and you get one roll each time you step beside one: `Int(Rnd * 100) < fn122A(char, 5, False) / 5`, a fifth of your perception. Notice it and the wall is cleared and the door written in its place | **decompiled source**, MAZEMOD.BAS:1589-1607 |
| Walking through a door or a secret door records it outright - the reliable way to find one | **decompiled source**, MAINMOD2.BAS:3765-3800 |
| Stepping onto a square also records the east side of the square to your west and the north side of the square to your south, because you can see those from where you stand | **decompiled source**, MAZEMOD.BAS:1637-1686 |
| In fog none of the edges are recorded - only the features | **decompiled source**, MAZEMOD.BAS:1585 (`If fn0380(gc1D3E, l027E) = False`), where `gc1D3E` is the fog bit |
| The far east and north edges of the grid record as walls | **decompiled source**, MAZEMOD.BAS:1618-1623 |
| Nothing is recorded at all while you are lost | **decompiled source**, MAZEMOD.BAS:1580 |
| **Rooms repopulate on a clock with no date.** The original compares `Abs(Timer - reference)`, and VB's `Timer` is seconds since midnight, so: a whole day away leaves a room exactly as you left it; coming back 23h40m later reads as twenty minutes *before* and the room stays empty; once the clock wraps past midnight every older room comes back at once; and a room emptied at midnight to the second stores a zero, which reads as "never set" so it repopulates on the next visit | **decompiled source**, `sub0A91` (MONSMOD.BAS:691-716): `Abs(Timer - Foes(0).M4EE6)` and `Or Foes(0).M4EE6 = 0` (703, 708); `DungeonState.RefillDue`. Dungeon_Respawn_Time_Formula documents all four as quirks. The port had subtracted two `DateTime`s and quietly fixed every one of them |
| The delay is `1200s + (5 x floor/2) x 60s` - 150 seconds a floor - **less** 5 seconds for every room on the floor emptied and not yet refilled (the header's area count less the level's tally), never under 120 seconds: 22.5 minutes on floor 1 and 57.5 on floor 15 with nothing emptied | **decompiled source**, `sub0A91` (MONSMOD.BAS:698-700; `DungeonState.RespawnDelay`), with the tally `fn0DDC` (2211-2215) taken down by a won fight (779) and put back by a refill (714-716). Dungeon_Respawn_Time_Formula has the five seconds *added* per spawn area, and the port had followed it (corrected; see `RespawnDelay`) |
| A room you emptied uses that delay; a room that **still has its monsters** uses **twice** it, counted from when they appeared. That is how ground you keep walking past changes what is standing on it | **decompiled source**, `sub0A91` (MONSMOD.BAS:701-705, `> (l01B4 * 2)`), the stamp set when the room is generated (`fn0D4D`, 1883) and when it is emptied (`sub0C53`, 776). Dungeon_Respawn_Time_Formula says the same ("2x +-respawnTime from the time the room was spawned"). The port had no doubling at all |
| **Characters have passwords**, kept in the character record in the clear (`M594C`). They are asked for when you load a character, and when you ask somebody else's character to join your party | **decompiled source**, CHARCREA.BAS:153-171, FRMMENU.BAS:221-233, MAINMOD2.BAS:3455 |
| Declining a password stores a **single space**. Every check asks `Len(RTrim(...))` first, so a lone space trims to nothing and no prompt ever appears - that is how "no password" is represented | **decompiled source**, CHARCREA.BAS:167-171 |
| The comparison is **case sensitive** and made against the right-trimmed stored value, unlike the character-name lookup immediately above it, which upper-cases both sides | **decompiled source**, MAINMOD.BAS:52 (`If p00A6 <> RTrim(gv14C2(pv00A8).M594C)`) against MAINMOD.BAS:37 |
| A side effect: a password typed with trailing spaces is stored with them but compared without, so it can never be typed correctly again - only the trimmed form works | **decompiled source**, the same line; the entered value is not trimmed, the stored one is |
| **The Library writes prose, not tables**, and tells you steadily more as the world learns more - four grades for a creature, two for an item | **decompiled source**, `fn1BBE`, MODULE11.BAS:78-406 |
| The Library's "This creature was last seen at X,Y,level, N days ago" is written every time the dungeon meets the creature - each group standing as a room is read, a group recognised for what it is, a character killed by one - with the party's first member's square (1-based) and the machine's date; a group taken for something else, or an invisible one unseen, is not filed. **Fixed 2026-10-15**: the port wrote the line only when the Library was first opened, from each member's own tally, with "level N" of where that member stood (always "level 1" in the City) and that day's date, and never again - so every creature read "last seen at level 1" on the day of that first visit. Records written that way are not rewritten: their place and date mend themselves the next time each creature is met, but the grades and "seen" counts that fold-in gave them (from kill counts, no source) stay - a grade only rises and a count only grows - so they can say more than the original would. The place is the square the party stands on now, not the port's record of it, which moves only at a change of area; and only a clock at 0 says "it is not known how many days ago" (HALL.BAS:224) - a record with no date counts from 0 | **decompiled source**, `sub06FE` (MONSMOD.BAS:3938-4006), called at MONSMOD.BAS:719 and 732 (sub0A91), FRMDUNM.BAS:497, and MONSMOD.BAS:2756, 2826, 3042, 3095, 3227; the page is HALL.BAS:219-233 and frmLibrary itself writes nothing (FRMLIBRA.BAS). The grade is `fn1272(..., False)` (MAINMOD2.BAS:2086-2108). `Librarian.FileSighting`, selfcheck "the Library's last sighting" |
| A chest's find goes in the Library: under the taker, at the level the taker reads it, with the lead monster it came from (or "unknown monster" when the first group is taken for something else, or is invisible to a taker without See Invisible), the date, the level found on ("?" when lost) and one more found - the item page's "Reports have it that N of these items have been found, the last one from a X on level L, D days ago." A level's re-reading of the pack files what it newly identifies, by grade and name only. A find read below Mostly is written at grade 0 and, like any record without a grade, is neither listed nor shown (FRMLIBRA.BAS:66, 75: "If l005A.IdLevel Then lstList.AddItem"). The lead is credited when the room's first group is known for what it is, as its record last stood - a group recognised and then killed keeps that (fn0F88 never clears KnownAs; sub0C53, MONSMOD.BAS:775-777, clears only Ref). **Added 2026-10-15**: the port filed neither, so no item page ever had that line | **decompiled source**, `sub0730` (MAINMOD2.BAS:4273-4328, no Rnd), called at MONSMOD.BAS:3840-3848 and FRMCTYG.BAS:434 (FRMCHAR3.BAS:550, the bank, was already filed); the page is HALL.BAS:378-394. The store restock's call (MAINMOD.BAS:1586) is ported too, 2026-10-18 - see the next row. `Librarian.FileFind`, `ChestFindFrom`; selfcheck "the Library files a chest's find" |
| Other adventurers' trading files in the Library too: each time the store restock actually puts an item on a shelf, the item is filed fully known (grade 4), with no first recorder if it had no grade, "an unknown monster", today's date, level "?" and one more found - so the item page reads "Reports have it that N of these items have been found, the last one from a unknown monster on level ? ...". A sale out of the store files nothing, nor does a full shelf. **Added 2026-10-18**: the port's restock filed nothing | **decompiled source**, `sub0730 m0070, -l02C4, m029E, -2, True` (MAINMOD.BAS:1582-1586) inside the add branch only; `m029E` is 0 (MAINMOD.BAS:75), `m0372`, fully known (MAINMOD2.BAS:4285-4286); the negated index marks a find with no finder (4280-4283: name "" at 4295-4301, "?" at 4314-4321); -2 is the unknown monster (4309-4312); sub0730 draws no Rnd, so the restock's draws are unchanged. `Librarian.FileRestock`, `StoreStock.Replenish`'s `shelved`; selfcheck "the store restock files what it shelves" |
| At grade 1 you learn only how big a creature is; at 2, its size and class; at 3, its full statistics and an admission that nobody knows what it can do; only at 4 its special attacks, its spells and its resistances | **decompiled source**, MODULE11.BAS:113-176 |
| An item withholds its damage modifier, whether it is cursed, the spell it casts, its effects, what it does when used and its per-guild use levels until everything is known | **decompiled source**, MODULE11.BAS:248-362, each guarded by `If l0064.M4EEE = mc0090` |
| The records are **shared** between every character - who first wrote a creature up, how many have been seen, how many explorers it has killed, where it was last met and how long ago | **decompiled source**, MODULE11.BAS:194-230, read from MDATA13 by `sub0F9A` |
| "How long ago" is whole days off VB's date serial, with `Abs()` again, and a zero means the record has no date: "it is not known how many days ago" | **decompiled source**, MODULE11.BAS:216-229 |
| **Four Character Options live on the character**, not in the settings file: Hide, Preload Char., No Perception Msgs. and Auto Change Lev. They are saved with them and each character carries their own | **decompiled source**, `FRMCONTR`, writing `M5914(0)`, `(2)`, `(3)` and `(4)`; the help's Control Panel page names all four, and its screenshot is `game_files/bm0.png` |
| Auto Change Lev is the only one on by default | **decompiled source**, CHARCREA.BAS:245, and the screenshot, where it is the one box ticked |
| **No Perception Msgs.** does not hide the message - it stops the roll. `mdrmazPerceiveSquare` leaves before it does anything at all, so the square is never examined | **decompiled source**, MAZEMOD.BAS:459 |
| **Hide**: "no other character's can pick you up and carry you anywhere" - a hidden body is not offered to whoever is standing on it | **help**, the Control Panel page |
| **Preload Char.**: the character is loaded at start-up "without verification of the password", so it walks straight past the one protection a character has | **help**, the Control Panel page |
| **Auto Change Lev**: with the map open, it follows you to the level you are on as you move (medDrawCursor); with it off the map stays on the level you are reading. It has no say in the level the map *opens* on: Form_Load opens on your level unless the level flag M59F4(0) is up or you are in town, and then on level 1 (corrected 2026-09-27; the port had also opened on level 1 with the option off or with only the square lost) | **decompiled source**, FRMAUTOM.BAS:94-98 and 309-318, and the help |
| **The pack is forty fixed slots, and slot 1 is the character's own Hands** - MDATA3's item 0, no attack, no defence, two hands wide. Empty slots keep their number: the list reads "7. Potion, 8., 9., 10." with the blanks still numbered | **decompiled source**, `M5857` throughout, `sub0F74`'s `If M5857(p).M4ED4 < 1 And p > 0` (MAINMOD2.BAS:1723); and a screenshot of 1.1 in play, where the Objects & Spells list runs to 21 with empty numbered slots |
| Something picked up goes into the **first free slot**, scanning from 1 | **decompiled source**, `For l04A4 = 1 To mc0190 - 1` (MAINMOD2.BAS:2941) |
| **Swap Items** (`s`) asks for both slot numbers in one box, comma separated - "Enter the Item Slots to Swap (i.e '12,17')" - and refuses anything without a comma, anything longer than five characters, slot 1, and anything past the fortieth. Empty slots swap perfectly well; that is how a pack is tidied | **decompiled source**, `fn1521`, MAINMOD2.BAS:3415-3437 |
| An equipped item is marked with a leading asterisk in the Items list | **screenshot** of 1.1 in play. The help's own shot (`bm14.png`) shows bold instead, so that is presumably 1.0 |
| **The Seer will locate a character**, not just creatures and items. The fee is `132567.52 * Log(level + 1) * level` with no random element, unlike his other two services - it is the depth they are at and nothing else | **decompiled source**, FRMSEEK.BAS:322-356 |
| He rolls `Int(Rnd * 100) + 1` against three times that depth, so the deeper somebody is the likelier he loses them. Within ten of succeeding he gets the level but "lost connection"; further off, "Another try may work, though." Finding them gives x,y,level, and adds "I also feel that they are among the dead" if they are | **decompiled source**, FRMSEEK.BAS:340-352 |
| Asking after somebody who is standing in the city gets "Do you mock my abilities?! They are here in the city!" | **decompiled source**, FRMSEEK.BAS:330 |
| **Shift-F6 writes CHARSTAT.TXT** to the game's own directory, and **appends** - ask twice and the file holds two copies. The original does not compose a report: it raises a flag and repaints the character window, and the routine that paints one line also writes it to the file, putting a Tab in front of anything not bold. So the file is the window's contents, headings flush left and the rest indented | **decompiled source**, `sub09B8`, MAINMOD2.BAS:3267-3275, and the help's Miscellaneous Keys |

## The source is now available

**The Options window, against the picture and the help.** game_files/bm3.png shows twelve tick
boxes, two volume sliders, Font Settings and File Backups. Every one is in the port, and the
captions come from the picture rather than the help's prose, which differs on two: the picture
reads **"No HELP Sound"** and **"No Autosave"**, the prose "No Help Sound" and "No AutoSave". For a
caption the picture wins.

**No Screen Saver button**, although the help describes one. Its own sentence explains why: "If you
purchased the CD-ROM/Enhanced version of Mordor and installed the Screen Saver, you can run the
Screen Saver configuration from the Options window." FRMOPTIO.BAS has no such control - its whole
control set is `chkCheck`, `cmdCommand`, `hsbVolume`, `lblBackup` and `lblLabel` - and neither does
the screenshot. The port has none. A help page describing a feature the shipped build does not have
is worth recording, because the obvious reading of that page is that something is missing.

**The `NoToolBar` slip is only harmless because of how INI keys match.** FRMOPTIO.BAS:221-224 writes
the toolbar setting under two different spellings: the unticked branch writes `NoToolbar` and the
ticked branch `NoToolBar`. MODULE1.BAS:969 reads `NoToolbar`. Windows INI calls match keys without
regard to case, so nothing ever came of it, and the port keeps the slip.

What makes that safe is not the slip but the port's own reader, which matches section and key
`OrdinalIgnoreCase` as Windows does. That is what `selfcheck` asserts - a file written with the
other casing, and one with the section in the wrong case too, both still read back. Reproducing a
fault is only safe while the thing that neutralised it is also reproduced.


**The help's Commands page, checked.** It lists 49 keys across game commands, the ten Ctrl tabs,
party keys, automap keys and dungeon commands, plus the click, double-click and right-click actions.
Every one is bound. That was traced by hand to its handler, and `Mordor.DataDump commands` prints
the list beside what the source still binds so the tracing does not rot.

The check is deliberately weak and says so. It scans the key handler's source for each binding
rather than pressing the key, because the bindings live in switch statements inside an Avalonia
window and driving them needs the window. It catches a binding being deleted; it would not catch one
wired to the wrong action. An interface check that looked stronger than it is would be worse than
none, so the scan also runs itself against a directory with no source and asserts that it then finds
nothing - which is the only way to know it is reading anything at all.


**Level 50 is how the data says "this spell comes from an item".** No guild reaches past 15 in any
school, so a spell of level 50 can never be learned. Four spells sit there, and every one of them
is carried by an item:

| spell | school | carried by |
| --- | --- | --- |
| Fate | Magical | Elixr of Fate, Magical Dust, Dalyn's Groan |
| She'tal | Damage | Scroll of She'tal |
| Life Domination | Bind | Orb of Life Domination |
| Shock Lance | Electrical | Lamurian Shock Lance |

The pairing is what makes it a convention rather than an accident. A level-50 spell with no carrier
would be a dead record and there are none; a carrier whose spell some guild could also learn would
not need the level. All six items are ordinary gear - floors 3 to 13, priced, charged, none cursed -
and all six offer their spell when held and identified, which is asserted rather than assumed.

This began as a loose end from the effect survey, which reported Fate and Life Domination as
unreachable and said nothing about She'tal or Shock Lance. That survey grouped by *effect*, and
those two share an effect with spells that are perfectly reachable. The right unit for the question
was the spell, not the effect - the same distinction that hid Charm of Opening behind an id.


**Record 0 means something different in each of the three tables**, and that is why the last two
bugs happened. "Skip record 0" looks like a rule about MDR files. It is not.

| table | record 0 | and so |
| --- | --- | --- |
| MDATA2 | **Charm of Opening**, a real spell, id 0, level 1 | never skip it. FRMCHAR3.BAS:83 iterates from 0 and skips nothing; all 105 records carry a name |
| MDATA3 | **Hands**, a real item - what you attack with bare-handed | never treat it as absent. It is pinned in pack slot 0, which is why the Objects & Spells window opens with "1. Hands". Keeping it out of the store and quest draws is right, because you cannot buy your own hands |
| MDATA5 | **GOBLIE**, a duplicate of Outcast Goblin | skip it. Its floor, hits, attack, defence, guild level, kind, rarity and trap flags are the same values, to the number |

The port now does all three, and `selfcheck` asserts what each record 0 is - including the
field-by-field identity that makes GOBLIE skippable - so the three cannot be quietly made
consistent with one another.

None of the three tables pads with nameless rows either, so a blank name is not the thing to filter
on in place of an id. There are none in any of them.


**The guildmaster levels look one out and are not.** Guild_Crests ends with a table of the twelve
guildmasters: "Nomad / Crashland (65)". The port seeds Crashland at 64, and every one of the twelve
is exactly one below the page's number. The page explains itself a few paragraphs earlier: "the
Nomads' guildmaster is Crashland (level 64). This means that as soon as you reach level 65, you will
become the guildmaster." The column is the level at which you take the chair, not the level of the
man in it, and the two agree once it is read that way. MAINMOD.BAS:621-632 has the names and levels.

The page writes the Healer's master as "Hands On Henry". MAINMOD.BAS:632 is `"Hands on Henry"`, and
the port follows the source.

**The crest table.** The same page gives each guild crest's stat mods, its Att/Def and its
abilities, and all twelve match. The abilities column is worth a note: it mixes two separate fields.
"Protect", "Invisible", "See Invisible" and "Levitate" are the item ability bits (`gv0684$`), while
"Electrical Resistance" and the rest are the item's resistance mask. Only the ability bits are
compared, so the Ninja's crest reads as having no abilities rather than as a mismatch - its
Electrical Resistance lives in the other field.


**The whole race table, checked.** The community's Races page carries three tables - statistics,
resistances and sizes - covering all nine races. Between them that is everything MDATA7 says about a
race: lifespan, experience factor, six stat ranges, which alignments it allows, its size and eleven
resistances. Every one of the 261 values matches.

It is worth saying what was tested before this: three lifespans, from the complications page, and
nothing else. The stat ranges, the alignment masks, the sizes and the whole resistance table were
being read by the port and checked by nothing.

The resistance columns are in `gv0620$`'s order - Fire, Cold, Electrical, Mind, Disease, Poison,
Magic, Stoning, Paralysis, Draining, Acid - so index 6 is Magic there exactly as it is in the
uncharmable-monsters check.

The page also states a rule rather than a number: "through the life of a character, you can only
improve a stat by 5 points above the listed maximum". MAINMOD2.BAS:6207-6214 clamps to
`Races(r).M553C(stat) + 5`, and `ItemUse` already does the same with the race minimum as a floor.

Nine rows matter rather than one. Reversing the resistance order in the comparison, to prove the
check reads the data rather than passing trivially, fails eight of the nine - Human is the exception,
because a row of zeroes reads the same backwards.


**Two monsters share id 307.** MDATA5 holds exactly one duplicated monster id: **Morgul** on floor 4
and **Shadow Ninja** on floor 16 both have id 307. Anything that resolves a monster by id - an
area's `LairId`, a `CompanionId`, a quest target - finds Morgul, because it comes first in the file.

No lair or companion in the shipped data points at 307, so this has no effect anybody can observe;
it is recorded because it is a genuine oddity in the data and because a join on monster id is
wrong without it. `Mordor.DataDump selfcheck` asserts the pair and which of them a lookup finds.
Companions and the Confinement pens, which the port had keyed by Id, are keyed by table index as
the original keys them since 2026-10-18 - see "Companions and pens are keyed by the table index".

**Correction.** This entry first claimed the duplicate was why the community's Rare_Monsters page
says "You also will never meet Shadow Ninjas, T'Manas, and Dragon Turtles". That was wrong, and the
reasoning was lazy: two facts about Shadow Ninja turned up in the same hour and I joined them
without checking that the spawn path uses ids at all. It does not. `fn0D4D` builds its pool from
monster *records* and weights them by Rarity, so a shared id cannot hide a monster from it.

What actually governs it is depth. The dungeon is fifteen floors and ten monsters carry a
LevelFound of 16 - Demon Prince, Flame Devil, Asmodeus, Reaper of Souls, Lamurian High Priestess,
Destroyer of the Deep, Phoenix, Black Gremlin, Shadow Ninja and T'Lanea. The encounter window needs
`LevelFound <= target`, and `target` is the floor you are on except on a Stud square, where it is
one deeper (MODULE4.BAS:1426-1495). So the only way to meet any of them is from a Stud on floor 15,
and then only where the area's spawn mask carries their class:

| monster | plain squares | Stud squares |
| --- | --- | --- |
| Shadow Ninja | 0 | 30 |
| Asmodeus | 0 | 18 |
| Reaper of Souls | 0 | 23 |
| Minion of Death (floor 12) | 974 | 50 |

Shadow Ninja is reachable from thirty squares, by the same route that leads to Asmodeus. Why the
community says otherwise is still open; `Mordor.DataDump shadow` prints the table above, and the
answer is not the duplicated id.

**The trap bit mapping, confirmed 400 ways.** Monster_Trap_Table gives every monster a YES or NO for
each of the eleven chest traps. Its column order - UNTR PSON XPLD DISE FIRE FEAR TPRT WTHR BLKT SLME
FATE - is *not* `gv08AA$`'s index order, which makes the table an independent reading of which
`TrapFlags` bit means which trap. Reading the page's columns as the bit order fits 151 of the 400
rows; the port's mapping fits all 400:

- 341 rows match bit for bit;
- 59 rows have `TrapFlags` of zero where the page marks UNTR, which is the same statement - a chest
  that can carry no trap is always untrapped;
- none differ.

Joining the two tables needs the floor as well as the id, for the one pair above.


**The rarity field groups exactly as the community tabulated it, all 140 rows.** Rare_Monsters lists
every monster of rarity 8 or below, grouped by that number and with its level. The page is careful
that the figure is an index and not a probability - "the exact chance being a complicated function,
but this serves as a nice index" - so what the list actually records is the Rarity field, M56DB.

All 140 rows match on both rarity and level, and the grouping is exhaustive in both directions:
exactly 140 monsters in the shipped data carry a rarity of 1 to 8, so the page covers the set with
nothing left over. That is 280 values plus the completeness check, and it is the only thing testing
M56DB - the rarity field is used by the quest re-roll and the store draw, and both of those sample
from it rather than checking it.

**A note on how this was nearly reported wrong.** One of the 140 names carries a byte above ASCII:
Gra�l Mosquito, 0xE8, which `Encoding.Latin1` decodes correctly. Comparing the lists by piping
the probe's output through a shell turned that byte into 0x8A on the way out, which looked exactly
like the port failing to decode the file - CP437 maps 0x8A to the same letter, so there was even a
plausible story about DOS codepages to go with it. The file itself settled it: the byte on disk is
0xE8 and always was. The assertion lives in C# and compares the string in memory, so no terminal
sits between the data and the check.


**The lair list checks out, and both of its disagreements are the page's.** Laired_Monster_List
tabulates every lair in the dungeon by coordinate. A lair is not a flag on a square - MDATA11 gives
each square an area id and each area a `LairId` - so a name on that page has to survive
square to area to monster, and all three readings have to be right for it to come out.

The page names 81 distinct monsters and the shipped data holds 81. Eighty are the same, and the two
that differ both have an answer in the data:

- **Destroyer of the Deep** is on their list, at 13,28,15, and lairs nowhere at all. It is the
  Lamurian High Priestess's companion - her `CompanionId` is its id - and she lairs at that square,
  so the page is recording where you meet it rather than what the area holds. The community's own
  uncharmable-monsters page says as much: it "will only spawn as a companion to the Lamurian High
  Priestess". Both halves of that are asserted.
- **Vomitis Goo** lairs on level 2 and is not on their list, with no companion linkage or anything
  else in the data to explain it away. That one looks like an omission on the page.

The coordinates themselves are not compared. `Mordor.DataDump lairs` reports the first square it
scans in each area, which is rarely the square the page names, and the lair belongs to the area
rather than to any one square of it - so the monster set is the honest comparison and the
coordinates are printed for reading rather than for checking.


**A published list that derives exactly, and the one name that does not.**
List_of_Uncharmable_Monsters_That_Can_Join names 45 monsters and states the rule behind them:
"either Charm Resistant, Resistant to All Magic, or have 100% Magic Resistance ... They are,
however, either Good or Neutral and thus may still offer to join your party."

Every term of that is a separate reading of MDATA5, so the list is worth far more than its names:

| the page says | the port reads |
| --- | --- |
| Charm Resistant | `SpecialPropertyFlags` bit 3 - `gv08DC$`'s own fourth string |
| Resistant to All Magic | `SpecialPropertyFlags` bit 2 - its third |
| 100% Magic Resistance | `Resistances[6]`, Magic by `gv0620$` |
| Good or Neutral | `AlignmentValue` other than 2 |

Applying those four to the shipped data picks out **44 monsters, with nothing spurious and nothing
else missing** - the page's list less one name.

That name is Aboleth, and the page carries it with its own note: "normally charmable, but never
appears outside of its anti-magic lair". So it is on their list for a reason other than the stated
criterion - where it lives rather than what it is - and it is correct that the criterion does not
derive it. The data says Aboleth is charmable, and the data is right.

`Mordor.DataDump selfcheck` asserts the whole set by name. Changing the Charm Resistant bit from 3
to 1 drops it to 31 and pulls in Brown Tyke, Charmer, Dark Shape and others, so the check is
sensitive to each of the four readings rather than just counting rows.


**Resistance rounds off spell damage; it does not truncate it.** `monGetSpellHits`
(MONSMOD.BAS:2464-2533) keeps the running damage in `l0432 As Integer` and takes resistance off with

```
l0432 = l0432 * (1 - (Chars(c).Resistances(SpellDefs(s).M5B5B) / 100))
```

so the assignment rounds it back into the Integer, half to even. The community's
Spell_Damage_Formula writes the step as plain multiplication because the rounding lives in the
declaration rather than the line, and the port had followed the page: `(int)(dmg * (1 - r/100.0))`,
which truncates.

One routine serves both directions - MAINMOD2.BAS:1467 is a character casting and MONSMOD.BAS:1046
a monster - so the same rule applies whoever throws the spell. The port already rounded on the
monster side, in `MonsterAttack.MonsterSpellHits`, and now does on the character side too.

Firebolt at spell level 15 is 26 damage with the roll at its best. Against 25% resistance that is
exactly 19.5, which goes to 20; against 35% it is 16.9, which the port used to report as 16.

**This is the third place the same VB conversion mattered**, after the questing rolls and the store
and pen draws. A sweep of every division assigned into a declared Integer or Long found 67 sites;
this was the one the port had wrong. The six monster breath-damage sites round correctly, including
again on each resistance step, and `sub0A64`'s halving weights (MULTIMOD.BAS:307-348) differ from
truncation only from the seventh party member onward, which no party reaches.


**The store and the pens draw a kind half-uniformly, and index 0 pays for it.** Both restock
routines pick what to stock the same way:

```
l027E = (Rnd * NumMonsterClasses)      ' MAINMOD.BAS:1337, mdrReplenishCStore
l02B8 = (Rnd * NumItemTypes)           ' MAINMOD.BAS:1598, mdrReplenishStore
```

with both variables declared `As Integer`, so each assignment rounds half to even rather than
truncating. That is not the uniform draw it resembles:

- index 0 owns only the values below 0.5, so it comes up **half as often** as every other kind;
- index `n` is reachable, matches nothing, and the original's `Do` loop simply draws again - so
  about the same share of draws are spent on nothing.

Measured over 200,000 draws with 24 kinds: index 0 at 2.07%, its neighbours at 4.19% each, and
2.06% falling off the end. Item type 0 is the Hands, so those stock half as often as any other type, and
monster kind 0 is the Humanoids, who are penned half as often. Both of those are checked rather
than assumed: six of the seven Hands can actually reach a shelf - Gnarled Hands is cursed and never
could - and 47 Humanoids can be penned, so the halving bites on real stock in both places rather
than on an empty slot.

The port had `rng.Next(n)` in both places, a clean uniform index, which quietly evens out a
distribution the game does not have. `Rules.RoundedIndex` is the shared draw now and `selfcheck`
asserts the shape, because a uniform index passes every test that only looks at *which* items appear.

This is the same VB conversion that the questing page tripped over. Worth knowing as a class: in this
source, `x = Rnd * n` with `x As Integer` is never the index range it looks like. A sweep of every
such assignment found twenty-one, and the rest were already right - the six monster breath-damage
sites in MONSMOD round at every step, including again when resistance is applied, and the port has
carried that correctly for some time.


**Three of the questing page's percentages are half a point out, and one of them is not.** The
Questing_Formula page lists a 6% chance the Questmaster lets you off, a 1% chance of a quest even
with the guild's questing percentage hacked to zero, and a 74% re-roll on a rare monster. Its
author reasoned from an integer roll - "<= 5" out of 0..99 being six values - which is the right
instinct and the wrong arithmetic.

`Dim l010E As Integer` (FRMCTYG.BAS:211) means `l010E = Rnd * 100` **rounds** on the way in, VB's
half to even, so `l010E <= 5` is `Rnd * 100 < 5.5` - 5.5%, not 6%. The quest-at-zero case is 0.5%,
not 1%. The port had neither: it compared the raw Single, giving 5% and very nearly 0%. Now it
rounds.

The re-roll is different in kind. FRMCTYG.BAS:517 tests `((Rnd * 100) > 25)` inside the expression,
with no integer to round it, so that one really is three times in four. Same for the item version at
FRMCTYG.BAS:558. The page applied its assumption where there was nothing to assume.

Everything else on the page checks out as written: the tier is `CInt(level / 25)` clamped 1..10, an
item quest squeezes it to `Int(tier / 1.5) + 1` so nobody is ever sent deeper than level 7, level 5
is a hard floor, item quests need level **above** 15 rather than the page's "15 or above", and the
four item exclusions - under 100 gold, cursed, level 0, and rare three times in four - are
FRMCTYG.BAS:552-560 exactly.

**The quest target is drawn the original's way, quirks and all** (corrected 2026-10-18). The port
drew a kind uniformly, then any monster or item of that kind at or above the tier, and gave up
after 60 tries, so a quest could fail to appear. FRMCTYG.BAS:489-561 does something else, and
`Rules.QuestMonster` and `Rules.QuestItem` now do it line for line and draw for draw:

- **Monster** (489-521): a class `Int(Rnd * NumMonsterClasses)`. If `Int(Rnd * 100) < 40` the run
  starts at the class's head; otherwise at the **first monster of the class at or below the tier**,
  so most targets are of the quest's own level (2,663 of 4,000 in the selfcheck's sample). The run
  ends one before the first monster deeper than the tier, or one before the class's last record,
  which so is never named on that path. The pick is `CInt(Rnd * (last - start)) + start`; when the
  first monster at the tier is already deeper than it, `last` is one before `start` and the
  negative span can round back a record, into the class before, which is then taken if it is
  shallow enough. The pass is redrawn when `Monsters(last).Floor > tier` or the rare test fails.
- **Item** (523-561): a kind `Int(Rnd * NumItemTypes)` drawn until its first item in the file is
  within the band; the run goes on while the band and kind hold, stopping one short of the file's
  last record. The pick is `Int(Rnd * (last - first) + 1) + first`: the `+ 1` inside the `Int`
  means the run's **first item is never named**, and a run of one item names the record **one past
  it** - the next kind's first item, or a deeper one of the same kind - if it passes the four
  exclusions. The record one past the file's end is the blank one `ReDim Items(gv1BEC)` leaves
  (MODULE1.BAS:730), worth 0, and so turned down.
- VB's `And` does not short-circuit, so the rare test's `Rnd * 100` is drawn on **every** monster
  pass and every item pass that reached the fourth `ElseIf`, rare or not. The port drew it only for
  a rare one.
- Neither loop has a cap; both end with probability 1 for the shipped data (selfcheck: 8,000 draws
  across the ten tiers, none empty).

Selfcheck "guild quest targets (FRMCTYG.BAS:489-561)", with scripted draws for each shape.


**Single precision is part of the rule, not an artefact of it.** The guild A/D tables were matching
305 of the community's 334 published values, and every one of the 29 misses was the port reading one
*higher* than the table. Two details fix it, and only both together.

`Chars(Me).GuildsJoined(g).Att` is a Single, and `modSetNewAD` adds `GuildDefs(g).Att` to it on
every award (FRMCTYG.BAS:1019). The figure on screen is its whole part.

A Warrior gains 3.3 attack and 3.6 defence an award. Ten awards is exactly 43.0 / 46.0, and the
published table says **42**/46 at level 40. In Single precision 3.3 is 3.29999995..., so the tenth
award lands on 42.99999952, whose whole part is 42. The defence accumulates a different rounding
error and really does reach 46. The published table is not approximate here; it is recording the
float error, and it is the only reason those two columns disagree.

Neither half of the fix works alone, which is what had hidden it:

| accumulator | shown as | published values matched |
| --- | --- | --- |
| double | rounded | 305 of 334 |
| Single | rounded | 305 of 334 - the rounding hides the very error that produces the 42 |
| double | truncated | 301 of 334 - now losing values *below* the cap that were right before |
| **Single** | **truncated** | **334 of 334** |

`fn13D4`'s own accumulator (MODULE6.BAS:2675, `l042E As Single`) is a Single for the same reason and
is now one too, though on its own it changes nothing - the interval arithmetic rounds to an Integer
each step anyway.

The comment that used to sit on this code argued the opposite - that truncating "would lose a whole
point wherever the accumulation lands on 17.999998 instead of 18". That is exactly what happens, and
it is exactly what the original does. The evidence for the wrong conclusion was real: with a double
accumulator, truncating *does* lose values. It was the pair that had to change.

`Mordor.DataDump selfcheck` now asserts all 334, and `adcheck` names any value that parts company.


**Where a community page and the source disagree, and the source wins.** Two of the pages state a
formula slightly differently from the code they describe. Both are transcription slips in the page,
not faults in the game, and the port follows the source.

*Trap_Identifying_and_Disarming_Formulas* writes the identify chance as starting from `Int(Wis/4)`.
MONSMOD.BAS:4134 is

```
l0630 = (Chars(Me).Stats(mc0640) + Chars(Me).StatMods(mc0640) / 4)
```

which is Wisdom **plus a quarter of the wisdom from gear**, not a quarter of the total. The same
precedence appears in the perception ability (MAINMOD2.BAS:1966). At 18 natural Wis and nothing worn
the page gives 4 and the game gives 18, so this is not a rounding quibble.

The page is right about something that looks like an error, though: "X = Chance for successfully
disarming the trap, without the max 98% cap". MONSMOD.BAS:4140-4145 really does use the uncapped
figure in `Int(l0630 + (l0636 / 1.2#))` and only then applies `If l0636 > 98 Then l0636 = 98`, so
the cap lands after the value has been consumed. Reproduced.

Three of the lines in that routine are dead. `l0634`, `l0604` and the first assignment to `l0636`
are all computed and then thrown away, the last by being overwritten with
`abilityChance(Me, m04E0, False)` on the very next line. The port does not carry them; there is
nothing to carry.

*Character_Attack_Formulas* gives the chance to hit as "range [1 to 97]" where `fn0DEE`
(MODULE4.BAS:2221-2236) clamps to 2..98. Both are correct: the code holds a threshold and misses on
`Int(Rnd * 100) + 1 >= threshold`, so a threshold of 98 is a 97% hit rate. The page counts hits.


As of 2026-09-18 the game's own Visual Basic source is decompiled and kept in
[`port/decompiled/`](../decompiled/) - 46 `.BAS` files plus `MAIN.TXT`. See
[DECOMPILE.md](DECOMPILE.md) for how, and for the first formulas read out of it.

**This supersedes guesswork for every row below.** Each one should now be replaced with
what the source actually does, rather than left invented or waiting on a published page.
Rows already confirmed against the source are marked.

## Invented rules, and the hunt for them

**A spell's sound went by a school table of the port's own** - **corrected 2026-09-28**. The port
played FIRE, COLD, SHOCK, POISON (Poison), DRAIN (Death), BETTER (Heal), TELEPORT (Movement) or
MAGIC by school. No line of the original has that table. `sub1005` (MUSIC.BAS:171-196) is the
only routine that sounds a spell, and it goes by `SpellDefs.Kind` (MDATA2's Category) and Level:

```
Select Case SpellDefs(p013E).Kind
    Case m0148  : sub09DE gc203C     ' Kind 0 (m0148 is a Static never assigned): 28 FIRE.WAV
    Case mc014A : sub09DE gc2046     ' Kind 2: 33 SHOCK.WAV
    Case mc014C : sub09DE gc2058     ' Kind 1: 42 COLD.WAV
    Case mc014E : sub09DE gc205C     ' Kind 9: 44 MAGIC2.WAV
    Case Else   : sub09DE gc200C     ' 4 MAGIC.WAV
End Select
Kinds 0, 2, 1 with Level >= 7: sub09DE gc205A   ' 43 EXPLOS.WAV as well
```

The numbers are MAIN.TXT:766-788's Global Consts, and the files MODULE1.BAS:308-379's
`SoundFiles` table. That table now lives once in the port (`SoundFiles`, Mordor.Core/SoundFiles.cs),
and the Credits window draws its random sounds from it too. So Poison, Death, Movement and every
other school play MAGIC, and Heal plays MAGIC2. A Fire, Cold or Electric spell of level 7 or
higher adds EXPLOS. No Sound FX silences both sounds, as it does every sub09DE sound.

The original calls it in six places, and the port now does too:
- **Passwall.** At the start of the case, rock or no rock (MAINMOD2.BAS:864-866).
- **Teleport and Displacement.** After a landing that did not kill (1134, 1152).
- **After the non-combat Select.** For the character in command, and not for those three
  (1397-1403). That covers the sheet's Cast, a buffer key and an item's spell alike. In a fight it
  is a heal or cure from the character in command, and nobody else's.
- **A damage spell in combat.** Once for each group it reaches, before the damage, under the
  No Party Sound test (1452-1455). That goes for a character, an item order and a companion
  alike. The port carries it on each group's caption.
- **fn0FF5, when a monster or a companion picks its spell** (MONSMOD.BAS:245). It has no No Party
  Sound test. The call is `sub1005 l00CC`, and l00CC is the chosen spell's place among the
  candidates, not the spell (`l00D4(l00CC)`). So what plays is the sound of SpellDefs(l00CC), a
  spell near the top of the table. Reproduced. The port had no sound here, and none for a spell
  cast in a fight either.

The original mixes the EXPLOS over the first sound through WAVMIX16's channels, and so does the port now (see
"Sound effects: WaveMix's eight channels"): each sound has a channel of its own. `selfcheck` reads
sub1005, MAIN.TXT and MODULE1 and holds the port to them for every kind and level.

**The monsters' turn was half silent in a timed round** - **corrected 2026-09-28**. The window
has two ways through `sub0E85` (MONSMOD.BAS:2593-3417). One is the timed round (PlayRound over
RoundPaced). The other is the engage and move tick (MonstersRound over MonsterAttacks). The engine
yields a line with no text, on the log channel, whenever a sound has no caption to go with it. The
timed round skipped those lines before their sounds. It played only a monster's spell pick, and
MonstersRound played all of it. So in an ordinary fight these were never heard: a companion's
death cry, a monster's spell, and DRAIN, STONE, STEAL and BYE for a member not on screen. A
companion's breath or spit on the monsters was silent too. Now every line goes through one rule,
`CombatSounds.Of` (Mordor.Core/CombatSounds.cs), on both paths. What `sub0E85` plays, in order,
and whether the `(M5F48 = True And pv0442 = Me) Or M5F48 = False` test guards it:

| Where | Sound | No Party Sound test |
|---|---|---|
| breath, each companion it kills (2723-2725) | sub0CB7 death cry by kind | yes |
| breath on the character (2739-2740) | FIRE | yes |
| acid, a companion it kills (2799-2801) | sub0CB7 | yes |
| acid on the character (2808-2809) | SPIT | yes |
| spell pick, fn0FF5 (245) | sub1005 of SpellDefs(l00CC) | **no** |
| spell cast (2847-2849) | sub0D35 attack cry | yes |
| spell that hurt (2850-2852) | sub0E99: RIP for kinds 2, 3, 4, 7, then OUCH | **no** |
| spell, each companion it kills (2861-2863) | sub0CB7 | yes |
| a blow that kills a companion (2966-2968) | sub0CB7 | yes |
| the turn's result, if the character was targeted (3009-3018) | sub0D35, then RIP / CHIT / BSTAB for Lethal / Critical / Backstab | yes |
| the character hurt (3026-3027) | sub0E99 | yes |
| electrocution of the character (3077-3078) | SHOCK | yes |
| electrocution kills a companion (3103-3105) | sub0CB7 | yes |
| death of the character (mdrcharSetDead, MAINMOD2.BAS:4558) | DIE (sub086D) | no |
| ageing (3130-3131) | WITHER | yes |
| drain (3183-3184) | DRAIN, after the "drained" line | yes |
| stoning (3213-3214), a companion also (3235-3237) | STONE, then sub0CB7 for a companion | yes |
| poison, disease (3251, 3268) | POISON, DISEASE | yes |
| paralysis (3287-3288) | PARAL | yes |
| item destroyed (3318-3319) | SLIMETR | yes |
| theft (3369-3370), the thief leaves (3377-3378) | STEAL, BYE | yes |
| the cry for help (3406-3407) | HELP, under No HELP Sound (M5F71) alone | no |

No Sound FX (M5EFC) silences all of them, in the two players themselves: sub09DE
(MUSIC.BAS:143) and sub086D (MUSIC.BAS:204).
The monster's melee hit is its attack cry and OUCH. HIT is the character's blow (MODULE4). Three
guards were also wrong, besides the silence:
- **sub0E99 after a spell.** The port had put it behind the No Party Sound test with the rest of
  the spell. It now has its own line with no actor, so it is heard with the box ticked.
- **WITHER, PARAL and SLIMETR** for a member not on screen. They rode on message lines with no
  actor, so they were heard with the box ticked. Those lines now name the member.
- **The STONE after the round.** The round's end played STONE whenever anybody in the party was a
  statue. That was every round a statue stood there, and a second STONE on top of the blow's own.
  It is gone. The DIE at the round's end now plays only for a death not already heard.

`selfcheck` plays scripted monster rounds down both paths. A melee blow is GOBLIN, OUCH (DOG, RIP,
OUCH for an animal). A companion's death is its cry, once. A drain on the other member is DRAIN,
and No Party Sound silences it. Another's blow is silent under No Party Sound, but a spell on them
still plays its pick and OUCH. The check also reads PlayRound, to see that the log-only lines reach
LineSounds.

A related gap was open until **2026-09-28**, and is now **corrected**. In `monCompanionAttack`'s
mishaps, a companion hurts a party member or their companions. The port played none of its death
cries for those companions. It also left out the companion's own attack cry after every blow, and
three of the guards were wrong. What `monCompanionAttack` (MONSMOD.BAS:806-1360) plays, in order,
and whose No Party Sound test guards it. "Owner" is `pv01E4 = Me`, "victim" is `l0214 = Me`:

| Where | Sound | Test |
|---|---|---|
| breath on a party member (890-891) | FIRE | victim |
| breath, each of the victim's companions it kills (913-915) | sub0CB7 death cry by kind | owner |
| breath on the monsters, each hit (931-935) | FIRE | owner |
| spit on a party member (974-975) | SPIT | victim |
| spit kills a companion (996-998) | sub0CB7; an empty slot's Monsters(-1) errors instead | owner |
| spit on the monsters (1011-1013) | SPIT | owner |
| spell pick, fn0FF5 (245) | sub1005 | **none** |
| spell mishap, each companion it kills (1068-1070) | sub0CB7 | **victim**, unlike the rest |
| blow kills a monster (1175-1176) | sub0CB7 | owner |
| blow kills a companion (1201-1203) | sub0CB7, then Exit For: no attack cry | owner |
| after every other blow, a miss included (1212-1213) | sub0D35 attack cry | owner |
| stone (1236-1237), a companion also (1256-1258) | STONE, then sub0CB7 for a companion | owner |
| electrocution (1276-1278), a companion killed (1314-1316) | SHOCK, then sub0CB7 | owner |

Each plays once, on a line of its own or on the line it goes with, through `CombatSounds.Of`. The
guards that were wrong:
- **STONE** on a party member or their companion had no actor, so it was heard with the box ticked.
- **SHOCK** on a companion had no actor either.
- **SHOCK** on a party member rode on the "You were accidently electrocuted" caption. That caption
  goes to whoever is on screen (1300), so SHOCK followed the watcher and not the owner. It now has
  its own line, ahead of the caption, and the caption is silent.

The blow's attack cry and a monster's death cry at the blow were missing on the monsters too. They
are the same two statements, whoever the blow lands on. `selfcheck` scripts a dragon companion
killing another member's one-hit undead. That plays DI-UNDEA once, after the line, heard by the owner
and not by the victim under No Party Sound, and no DRAGON. A blow on a member that they survive plays
DRAGON once, under the owner's test. A companion's Blue Flame that goes astray and kills the undead
plays DI-UNDEA once, before its line, heard by the victim and not the owner.

**Banish and Dispell hit everything** - **corrected 2026-09-22**. A spell whose effect is 75 to 99
only harms one class of creature. `monGetSpellHits` (MONSMOD.BAS:2561-2565):

```
Select Case SpellDefs(p0422).Ailments
    Case mc043E To 99
        If  MonsterClasses(Monsters(Foes(CombatOrders(pv0420).M547A).Ref).Ref).M550A <> SpellDefs(p0422).Ailments - 75 Then
            l0432 = 0
        End If
End Select
```

with `mc043E` = 75. That routine serves both directions - MAINMOD2.BAS:1467 is a character casting
and MONSMOD.BAS:1046 a monster - and the port applied the rule only on the monster's side.

Five spells carry both damage and a class: Dispell Undead, Dispell Elemental, Banish Devil, Banish
Demon and Abolish Undead. Cast by a character, every one of them hit anything at all, for full
damage. Over twelve casts at spell level 30, Abolish Undead did the same 10,283 to a creature of no
relation as it did to the undead it names.

The class read is the **ordered target's**, not each victim's: the expression indexes
`CombatOrders(c).M547A`, the group the caster aimed at. That only differs for a group spell, where
the original still asks the one group.

The charm, control and dominate spells in the same effect range are not part of this - they take
the charm path rather than the damage path.

**Nobody could learn Charm of Opening either** - **corrected 2026-09-22**. The same confusion as the
seven lock-opening items, in the other half of the same file. `Spellbook.Known` skipped
`s.Id == 0`, so the spell the help uses as its own worked example was in no character's spellbook.

MDATA3 and MDATA5 do have a template at record 0, and their loaders are right to skip it. MDATA2
does not: record 0 is Charm of Opening, level 1, effect 30, and all 105 spell records carry a name.
FRMCHAR3.BAS:83 runs `For l0076 = 0 To gv1BF8 - 1` and skips nothing. Exactly one record has id 0,
and it is that spell - so the guard filtered no padding at all, only the spell itself.

Between the two bugs, every route to a locked chest was shut - and there is no other route.
Charm of Opening is the **only** spell in the shipped data with a lock-opening effect: the Opening
school holds two spells and the other is Fate. So for as long as both gates were in place, no
character and no item could open a locked chest by magic at all.

Both routes are now checked end to end rather than at the level of the spell list - a Thief 10 can
cast it, and all seven items offer it when held - because the arithmetic assertion on this very
spell passed throughout while the spell itself was unreachable.

**Worth noting against my own earlier work.** The help's worked example - "the Charm of Opening
spell, which is Spell Level 3 for the Thief's guild, but Spell Level 12 for the Nomad's guild" - is
asserted earlier in this file, and it passed the whole time. It tests `ShownSpellLevel`, which is
the rate arithmetic, and the arithmetic was right. Nothing asked whether the spell reached a
spellbook. The check now goes through `Known` and tests both sides of both boundaries: a Thief at 4
and 5, a Nomad at 22 and 23.

**And a third gate, behind the two above: the lock could only be cast at while monsters stood -
corrected 2026-10-02.** `mdrcharCastSpell`'s opening case (MAINMOD2.BAS:1050-1058) reads
`Fight.ChestLock`, the loaded area's own record, which outlives the monsters: you kill the group,
see "Magically Locked", cast the Charm or use a Key of Opening, and the caption clears. The port's
`CastHere` read the lock off `_combat`, and the window lets go of `_combat` the moment a fight is
won, keeping the chest in `_chest` - the field `OpenChestBy` already reads. So every opening spell
and item answered "There is no locked chest here!" from the last monster's fall onward, which is
when anyone casts one. The branch now reads the live fight or, failing that, the chest held for
this area (`FightIsHere`, as monOpenChest has it), and redraws the chest after the lock springs
(`monShowChest`, 1058). Seen in play on level 2: a Key of Opening equipped and used at a
"Magically Locked" chest with the room cleared.

**Every lock-opening item in the game did nothing** - **corrected 2026-09-21**. The spell an item
casts is `M56C4`, and two things about that field matter: it is one-based, and it holds a *record
position* rather than a spell id. `sub15CD` tests `Items(i).M56C4 > 0` and then uses `M56C4 - 1` as
the index into SpellDefs (MAINMOD2.BAS:6112 and 6127).

`ItemUse` and `Librarian` read it that way. `Spellbook.Castables` did not - it gated on a different
field, `SpellId > 0`. Charm of Opening is the spell record at index 0, so every item that casts it
has `M56C4 = 1` and a spell id of **0**, and that gate dropped all of them:

- Thief's Cap
- Ring of Opening
- Dust of Opening
- Key of Opening
- Skeleton Key
- Crest of the Thief
- Crest of the Scavenger

Seven items, every one of them a lock-opener, and not one of them could be used. The Key of Opening
and the Skeleton Key exist for nothing else at all.

The community's Guild_Crests page is what turned this up: its second table says the Thief's and
Scavenger's crests cast Charm of Opening, and the port said they cast nothing. Two rows of a
twelve-row table, and behind them five more items the table does not mention.

`Mordor.DataDump selfcheck` now asserts what all twelve crests cast, and names the seven items a
`SpellId` gate would drop - so the distinction between the two fields is written into a check rather
than into a comment somebody can read past.

**A failed raise spell cost nothing at all** - **corrected 2026-09-21**. `fn16B5`
(MAINMOD2.BAS:330-380) resolves every raise - the three spells and the morgue alike - and a failure
goes to `raiseComplications` (MAINMOD2.BAS:1642-1685), which drags the character back anyway at a
price: five off every stat down to the racial minimum, fifteen years of age (`mc02EA` = 5475 days),
`MaxHits = Int(MaxHits / 1.2#)`, and anything the reduced stats can no longer carry taken off.

The morgue in the city had all of this. The raise **spells** in the dungeon had none of it - a
failed cast returned "Raise Failed!" and stopped, and a successful one charged nothing. Both now go
through `Mordor.Core.Raising`, so the two cannot drift apart again.

Three details of the shared routine are worth stating because a reader will assume otherwise:

- **A raise ages you even when it fails.** 54 days ordinarily (`mc00B4`), 84 after petrification
  (`mc00C4`), 124 after being pulled from rock (`mc00B8`), and 5,475 more on top if there were
  complications. The quiet failure - the one that leaves you dead - still charges the 54.
- **Being pulled into rock always complicates.** MAINMOD2.BAS:332-334 zeroes the chance before the
  roll and MAINMOD2.BAS:1651 tests `Or DeathState = mc00B6` afterwards, so that death cannot be
  raised cleanly by any route that gets as far as rolling.
- **Only the morgue charges the Constitution.** The `(50 - Con * 2)` roll is guarded by
  `Chars(c).M58DC = mc00C6`, and `mc00C6` is 23 - the morgue's own raise kind, not one of the three
  spell effects. The community's page states it of "a successful raise" without that qualification.
  Petrification is different: that costs a point on any raise, MAINMOD2.BAS:352-355.

**Neither Constitution loss stops at the racial minimum** - **invented guard removed**. The port had
`Math.Max(1, ...)` on the petrification loss and `&& Stats[Con] > race.Min[Con]` on the morgue's.
MAINMOD2.BAS:353 and :359 are both a bare `Stats(mc00C2) - 1` with nothing below them. Only the
*complications* stat drop is floored, at MAINMOD2.BAS:1658-1660, and only there. A character raised
often enough really can be taken below what the race can roll.

**"Constution".** MAINMOD2.BAS:358 misspells it, and the line names the character rather than
addressing them: `"MORGUE: " + Chr(34) + RTrim(Name) + Chr(34) + " lost a point of Constution during
the raise."` The port said "MORGUE: You lost a point of Constitution during the raise.", which is
neither the wording nor the spelling. Reproduced.

**A second guild made your spells dearer instead of cheaper** - **corrected 2026-09-21**.
`Spellbook.Cost` took the guild giving the *highest spell level* and priced the spell there.
`fn1485` (MAINMOD2.BAS:2868-2903) prices it in every guild the character belongs to and keeps the
cheapest:

```
If  SpellDefs(p047E).Level <= GuildDefs(l048C).M55F6(SpellDefs(p047E).Kind, 1) And Chars(pv047C).GuildsJoined(l048C).Level > 0 Then
    l048A = Int((Chars(pv047C).GuildsJoined(l048C).Level / 2) + .5#)
    l0488 = GuildDefs(l048C).M55F6(SpellDefs(p047E).Kind, 0)
    l0484 = l0484 * l0488
    If  Int(((l0484 / l048A) * SpellDefs(p047E).M556D) * l0488 * 1.9#) <= l0480 Or l0480 = False Then
        l0480 = Int(((l0484 / l048A) * SpellDefs(p047E).M556D) * l0488 * 1.9#)
```

The help says it in words, independently: "if the Wizard's guild can cast the spell for less points
than the Mage's guild, the lowest points will be used".

These are different guilds more often than they sound, because cost goes as `rate^2 / SL` and the
rate is squared in the numerator. A Wizard 40 who is also a Healer 28, casting Harm (a level 6
Wounds spell, ratio 40, Wizard rate 1.25, Healer rate 1):

| guild | SL | rated level | cost |
| --- | --- | --- | --- |
| Wizard 40 | `Int(40/2 + .5)` = 20 | `round(6 * 1.25)` = 8 | `Int(8/20 * 40 * 1.25 * 1.9 - .0001)` = 37 |
| Healer 28 | `Int(28/2 + .5)` = 14 | 6 | `Int(6/14 * 40 * 1 * 1.9 - .0001)` = 32 |

The Wizard has the higher spell level and the Healer is cheaper, so it costs 32. Joining a second
guild was making spells the first guild already knew more expensive.

Two smaller corrections came with it. The gate is the ceiling alone - `Level <= SpellMax` with any
level in the guild - not `CanCast`, the same weaker test `casterSpellLevel` uses. And `l0484` is an
Integer holding `Level * rate`, so a fractional rate rounds there (VB's half to even); the published
formula leaves `StartSpellLevel * BaseMod^2` uncollapsed, which agrees whenever the rate is a whole
number and differs slightly when it is not.

**The `- 0.0001` is gone: fn1485's own arithmetic - corrected 2026-10-18.** The page explains its
epsilon as "internal loss of precision when storing integers as floating-point decimals: 209 is
stored as 208.9999..., causing it to be rounded down to 208". The source shows where that comes
from: `1.9#` is the Double nearest 1.9, a shade under it (1.8999999999999999112), and `Int` floors
the product as the x87 holds it. A product rounded to a Double would not do it - 110 times 1.9#
rounds back to exactly 209 - and neither would rounding every step to its VB type (the Single
steps `l0484 / l048A`, `* M556D`, `* l0488`, then the Double product): that gives 209 too. So the
measured 208 says the expression is evaluated without rounding between steps, and
`Rules.Fn1485Price` floors the exact product `rated * M556D * rate * 19 / (10 * SL)`, one less when
it is a whole number. The rate is the guild record's Single (MAIN.TXT:209) as stored, and
`l0484 = l0484 * l0488` rounds half to even into the Integer.

The epsilon agreed with this wherever the true cost is a whole number or more than 0.0001 over
one. On the shipped tables (every spell, guild and SL 1-255) the two differ only for the ratio-0
spells, where the page's formula went to -1 and fn1485 gives 0; the cost is then the floor
`l0486` either way. A rate with a long Single fraction (none ship) is where they part: rate 1.1
at ratio 100 and SL 1 is truly 209.0000045, 209 to fn1485 and 208 to the page.

**Verified against the running original - 2026-10-25.** The alternative reading - every step
rounded to its VB type, Single or Double - raises about 6% of the shipped guild-spell-SL prices by
one, so the Points column of Objects & Spells (which is fn1485) in the real game, run under OTVDM
from a scratch copy, settles it. It matched `Rules.Fn1485Price` in every case read, and nine of
them discriminate, all for the x87 reading and none against:

| caster | spell | original | x87 reading | rounded-step reading |
| --- | --- | --- | --- | --- |
| new Human Sorcerer, SL 1 (Int 18, Wis 16) | Shock | 18 | 18 | 19 |
| Nomad 37 + Sorcerer 37, SL 19 | Charm of Opening | 143 | 143 | 144 |
| | Cold Blast | 2 | 2 | 3 |
| | Sight Veil | 2 | 2 | 3 |
| | See Invisible | 2 | 2 | 3 |
| | Mass Sight Veil | 35 | 35 | 36 |
| | Sphere of Flames | 74 | 74 | 75 |
| | Flesh to Stone | 95 | 95 | 96 |
| | Paralyzing Death | 89 | 89 | 90 |

The controls, which both readings give, matched too: Firebolt 26 at SL 1, and at SL 19 Poison 2,
Leprosy 9, Acidic Spray 23, Ice Spray 15, Hail Storm 52, Arctic Storm 119, Firebolt 1, Blue Flame 3,
Flamesheet 10, Pillar of Fire 31, Resist Fire, Resist Draining and Resist Cold 4, Resist Poison and
Resist Disease 7. So the x87 keeps an expression's intermediates unrounded until the store, as the
page's 208 had suggested - the reading the cast gate, fn12A1 and fn12B2 below rest on, which is
now measured rather than inferred.

`selfcheck` asserts the page's own initial-cost identity (a Sorcerer 1 pays 26 for Firebolt, which
is `Int(14 * 1 * 1.9 - .0001)`), the Wizard 40 / Healer 28 case above, the 209 that floors to 208,
the 209.0000045 that does not, a ratio of 0, the half-to-even `l0484`, and every price measured in
the original above ("measured: ...").

**The cast gate's `Level * rate` is read the same way - checked 2026-10-19.** mdrcharCheckCast's
spell-level test stores `SpellDefs(s).Level * l011C` into the Integer `l011E`, with `l011C` a
Single (MAINMOD2.BAS:421-422, 461). By VB's typing that product is a Single, but fn1485's
`l0484 = l0484 * l0488` is the same Integer-times-Single store, and the measured 208 above says -
and the original's prices, measured 2026-10-25, confirm - that the x87 keeps the product unrounded
until the store. So `CastGate.Unfit` rounds the exact product half to even, as
`Rules.Fn1485Price` does. The two readings part only for a rate with a long Single
fraction; the shipped rates are 1, 1.25, 2, 3, 4, 5, 6, 9 and 12, and `selfcheck` finds no guild
and spell of its school where they differ (0 of 290).

**The x87's 80-bit intermediates against the port's Doubles - scanned 2026-10-21.** `Mordor.DataDump
x87` evaluates every formula whose inputs are all shipped data three ways - the port's code, the
reading above step by step (each x87 operation exact and then rounded to a 64-bit mantissa, Log
correctly rounded to 64 bits, a Variant step rounded to its Variant's type, and every store rounded
to its variable's type, half to even) and exactly - and counts where the port and that reading give
a different whole number:

| formula | cases | differ |
|---|---|---|
| fn12A1, experience for the next level (MAINMOD2.BAS:2118-2138): every guild and race, levels 1-1000 | 108,000 | 0 (33,900 before) |
| fn030F, item use level (MODULE1.BAS:123-132): every item and guild | 4,392 | 0 |
| adInterval, the A/D stretch (MAINMOD2.BAS:2680-2707): every guild, levels 1-1000 | 12,000 | 0 |
| fn12B2, the guildmaster's gold for a level (MAINMOD2.BAS:2140-2155): every guild, levels 1-1000 | 12,000 | 0 (11,976 before) |

fn1485's price and the cast gate are exact already (above). The reading itself is the one the
original's spell prices confirmed (2026-10-25, above), so fn12A1, fn12B2 and the cast gate rest on
a measured reading rather than an inferred one - though that measurement read spell prices only,
not fn12A1's experience or fn12B2's gold. Everything else the port floors or rounds has a roll, a
character's own stats or hits, or gold in it.

- **fn12A1 - corrected.** Its last line, `l0392@ * (((Log(l038C) / Log(l038C + 1)) + 1) - .7# +
  (l038C / 25))`, is one typed expression, and the port narrowed `l038C / 25` to a Single as VB's
  typing has it. The x87 does not narrow it (the reading the original's fn1485 prices confirm), and 0.16 narrowed is
  0.1599999964: at high levels that is a few tenths of a point of experience, and a third of the
  cases came out one off. Unnarrowed, the port's Doubles agree with the 64-bit evaluation in every
  case - the loop's own steps go through the Variant counter `l0386` and are Doubles in the
  original too. The selfcheck anchors (Human Nomad 556,907 at 79 and 872,326 at 99) hold either way;
  the community's closed form, which was derived with the quotient unnarrowed, now parts from the
  routine in 12 of 16,800 cases to level 200 rather than 269.
- **fn12B2 - corrected.** `l03A8& + (((l039E / 2) *
  l03A6&) * 60.2#) / l039E` is typed throughout, and `60.2#` is a shade over 60.2
  (60.200000000000003). Every fifth term `(2i - 1) * 30.1` is a whole number and a half - 150.5 at
  i = 3 - which as a Double is the exact half and rounds to even, while the x87 keeps the shade and
  rounds up; `Gold / 10` (a Single to VB, unnarrowed on the x87) moves the last line too. So on the
  x87 reading the guildmaster asks one or more gold more at almost every level (Nomad level 3: 352
  rather than 351). `Rules.Fn12B2` now evaluates both Long lines on `X87`, a 64-bit-mantissa
  value, and the probe's own rational evaluation agrees with it on all 12,000 cases. The community's
  page rounds every half up as well, though with JavaScript's `Math.round`, so it is not a
  measurement; it now parts from the routine in 1,130 of 11,976 guild-levels, by one gold, where it
  had parted in 11,952 by up to 191. The page's own anchors - a level 755 Thief paying almost 33
  million, more than 8 billion spent to get there, nearly 19 billion to reach 999 - hold either way
  (32,599,873; 8.19 and 18.98 billion). selfcheck pins Nomad from level 3 at 352.

**The spell level a spell is cast at excluded guilds the original counts** - **corrected
2026-09-21**. `Spellbook.SpellLevelFor` is the SL that drives damage, healing and protection, and it
was gating each guild on the full `GuildData.CanCast`. `casterSpellLevel` (MAINMOD2.BAS:2852-2865)
uses a weaker test:

```
If  GuildDefs(l0474).M55F6(SpellDefs(p0472).Kind, 0) > False And GuildDefs(l0474).M55F6(SpellDefs(p0472).Kind, 1) >= SpellDefs(p0472).Level Then
    If  Chars(pv0470).GuildsJoined(l0474).Level > False Then
        If  Int((Chars(pv0470).GuildsJoined(l0474).Level / 2) + .5#) > l0476 Then
```

A guild counts if it has any rate for the school and its ceiling reaches the spell's level, and the
character has a level in it. The `Level * rate <= guildLevel / 2 + .5` gate that decides whether the
spell appears in the list is not applied here. The two differ whenever a character belongs to a
guild with a worse rate but a higher level: a Wizard 30 and Seeker 24 casting a level 4 Sight spell
get SL 15 from the original and got SL 12 from the port.

This also confirms the `guildLevel / 2 + .5` quantity a second time, from a different routine than
the spell list builder it was taken from.

**A spell arrived at half the guild level it should** - **invented, corrected 2026-09-21**.
`GuildData.CanCast` read `guildLevel >= Ceiling(spellLevel * rate)`, with no source behind it. The
character's spell list is built in FRMCHAR3.BAS:91-97, and the test there is

```
If  SpellDefs(l0076).Level <= GuildDefs(l0082).M55F6(l0078, 1) Then
    l007C = GuildDefs(l0082).M55F6(SpellDefs(l0076).Kind, 0)
    If  (SpellDefs(l0076).Level * l007C) <= ((Chars(Me).GuildsJoined(l0082).Level / 2) + .5#) Then
```

so the spell's rated level is compared against `guildLevel / 2 + .5` - the caster's **spell level**,
the same quantity `Rules.SpellLevel` already computed from the Spell_Damage_Formula page - and not
against the guild level itself. The port's version handed out every spell at roughly half the level
the original asks for: a Wizard reached the level 12 Teleport at guild level 12 rather than 23.

The help says the same thing in words, and independently: "Every character also has a 'spell level',
which is related to a character's current Guild & Guild Level... The power of a spell is based on a
character's spell level", and spells are learned at a spell level, not a guild level.

The number the spell list *displays* is a separate quantity, `Int(SpellDefs.Level * rate)`
(FRMCHAR3.BAS:110) - floored, not rounded up. That is `GuildData.ShownSpellLevel`, and it is what
the help prints in brackets after a guild name, by its own account: "the level at which the spell is
learned at by a particular guild is listed after the guild name". The help then works one example by
hand - "the Charm of Opening spell, which is Spell Level 3 for the Thief's guild, but Spell Level 12
for the Nomad's guild" - and Charm of Opening is base level 1 in MDATA2 with a Thief rate of 3 and a
Nomad rate of 12, so `Int(1 * 3) = 3` and `Int(1 * 12) = 12`. Both are asserted in
`Mordor.DataDump selfcheck`, along with the guild level 22/23 boundary either side of Teleport,
which is the one place the corrected rule and the invented one give different answers.

**Six lines the Bank never says** - **invented, removed 2026-09-21**. `FRMCITYB` has four strings
in it: "Cannot put equipped items in the bank!", "There is no more room left for another item!",
"You have N gold and M free Bank slots available." and a BANK ITEM SORTER line. The port also had
"Deposited N.", "Withdrew N.", "Deposited all.", "The party's gold is pooled into your account.",
"Everyone banks what they were carrying." and "Everything the party holds, banked or carried, is
now in your account." None of the six exists. `txtAmount_KeyPress` moves the gold and refreshes the
readout; `cmdCommand_Click`'s three party cases do the same; neither says anything.

The same routine also shows the refusal is a `Beep` and no transfer - `If l00E6 > Chars(Me).Gold Or
l00E6 <= 0 Then Beep`. The port clamped with `Math.Min`, so asking to deposit more than you were
carrying deposited whatever you had instead of refusing.


**"(not for your guild)" on a pack line** - **invented, removed 2026-09-21**. The dungeon's Items
page appended that to any item the character's guild cannot use. `mdrcharShowItem`
(MAINMOD2.BAS:5370-5480) appends nothing of the sort, and neither does `itemNameFor`; the row is
the number, the name, the alignment word, the charges and "(cursed)", and the only other things
that change are the bold and italic flags. It was also only on one of the two screens - the city
has always used `Loot.NameFor`, which is `itemNameFor` - so the same pack read differently
depending on where you stood.


The dungeon view, the last invented rule found by reading the source, was closed on 2026-09-18.
That was not the end of it: reading the port against the help file and the community formula
pages on 2026-09-20 turned up three more that the source alone could not have found, because a
rule the game does not have leaves no line to be wrong at - a Flee command, per-step water and
quicksand damage, and a Seer who identifies items. All three are gone. The lesson recorded here
is that "no invented rules" is a claim that has to be re-earned against every source of evidence,
not a box that stays ticked.

| Rule | Status | Evidence |
|---|---|---|
| Dungeon view: which squares are visible, which wall slabs are lit, which background column each half shows | **decompiled source** | MODULE3.BAS `sub0980`, lines 846-1288, 1291-1312 and 1315-1351, translated mechanically by [`tools/VbToCs`](../tools/VbToCs) into `Mordor.Core/MazeSelect.g.cs`. Regenerate with [`tools/gen-mazeselect.sh`](../tools/gen-mazeselect.sh); the translator emits `UNTRANSLATED` for anything it does not recognise, and the slice currently produces none |
| Dungeon view: where each of the eight wall slabs lands, and the water and quicksand strips | **decompiled source** | MODULE3.BAS lines 1354-1460, read into `Mordor.App/MazeView.cs`. Slab sizes and source columns from MODULE1.BAS:577-600 |
| Automap legend: "stairs / chute / pit / blue = water / grey = rock / orange = a door you have found..." | **invented, removed 2026-09-21** | FRMAUTOM carries four controls - picMaze, hsbLevel, lblLevel and lblInfo (FRMAUTOM.BAS:105-120) - and none of them is a legend; the help's AutoMap page has none either. The port printed a paragraph under the map explaining every colour, which took up a third of the window. Gone, and `lblInfo` - "You are at 8,10,1 facing North.", which the port never had at all - is in its place |
| Automap: fog takes a square off your map and says "Thick fog swirls around you; you cannot map this spot." | **invented, removed 2026-09-21** | Fog costs you the square's **edges**, not the square. MAZEMOD.BAS:1630 sets the mapped bit whether you are in fog or not, and only the wall and door bits sit behind `If testBit(BitDark, ...) = False` (1590-1596, 1646, 1671). The help says as much from the other side: the grey explore squares can make "other map items (i.e. fog, water, etc) ... look strange", so a fogged square is on the map to look strange. The original has no message for it at all - what fog does, it does to the Dungeon window |
| "A chest lies among the remains." - and the same sentence again in the chest panel, with "(o to open)" after it | **invented, removed 2026-09-21** | `monOpenChest` writes one of three bare captions into lblTrap and says nothing anywhere else (MONSMOD.BAS:3503-3510, 3650); picTrap itself carries lblTrap and lblDisarm and no third label (4171-4177). The port had wrapped each caption in a sentence of its own and put it in the combat log, and had written a line of explanation above the panel. The chest announces itself by being drawn |
| "You begin Firebolt, but the anti-magic field swallows it." - and the spell points went with it | **invented, removed 2026-09-21** | `mdrcharCheckCast` says "Anti-Magic area!" and leaves before anything is cast or spent (MAINMOD2.BAS:520-525) |
| "Anything worn stays put, and guild-issued gear cannot change hands. Below ground you can only pass gold you are carrying..." - the Exchange screen's instructions | **invented, removed 2026-09-21** | Form_Load builds pnlPanel(2) out of three sentences (FRMEXCHA.BAS:44-47): "To exchange Items, Drag and Drop an item label from one character to another. To exchange gold, Drag and Drop from the gold label to the other character's gold label and enter the amount. When exchange is complete, click on Save Exchange to save." The port had replaced them with a summary of its own refusals, and the screen had no dragging in it to describe |
| "Swapped; X looks over what they were handed.", "Given; ...", "N gold handed over.", "That is already theirs." | **invented, removed 2026-09-21** | An exchange that goes through says nothing at all; only the four refusals speak, and each of them is a MsgBox (FRMEXCHA.BAS:180-197) |
| Dungeon view artwork | **the game files** | MG11.DLL resources `MAZE`, `DOOR`, `BLNK`, `FOG`, `WATR`, `QSND`, extracted **by name** with [`tools/NeRes`](../tools/NeRes) and bound as MODULE12.BAS:77-103 binds them |
| Automap artwork: every icon on the map is the game's own | **the game files** | MG11.DLL resource `PICS`, 405x15, loaded by GRAPHICS.BAS:110 into `gv2BC6` and blitted by nothing but `medDrawSquare`. Never extracted until now; the map was drawn with lines, letters and colours of this port's own devising. Twenty-seven 15x15 cells, and `For l0104 = 0 To 22` over the square's flag word (FRMAUTOM.BAS:354) says what they are: **cell number = flag bit number**, so the sheet names itself against `Floor`. Cells 23-26 are the magenta arrow, `(Facing + 22)` with the original's one-based Facing |
| The two pictures either side of the monster in the Dungeon window | **the game files** | MG11.DLL resource `MISC`, 270x45, six 45x45 cells, loaded by GRAPHICS.BAS:91 into `gv2BBC`. Never extracted, and neither picture existed here in any form. `sub0B9B` (MONSMOD.BAS:4434-4471) draws the left one - a skull and crossbones while `Foes(0).Engaged = 1`, a peace sign while it is not, a demon's head in fire for Asmodeus - and `monShowChest` (4099-4106) the right one, cell `(2 * 45) + ((TreasureClass - 2) * 45)`, which is the class itself, or cell 4 for Asmodeus. The left one is the only thing on screen that tells a group standing on your square from a group fighting you |
| Asmodeus's own picture, which replaces the dungeon view when you meet him and stands in for his sheet cell in the Library | **the game files** | MG11.DLL resource `ASMO`, 100x100, loaded by GRAPHICS.BAS:112 into `gv2BB4`. The last thing `mdrmazShowMaze` does is ask `fn0C3B` whether the lead group is him and, if it is, blit this into picMaze2 instead of the maze (MAZEMOD.BAS:1478-1480; `sub0AA6`, MONSMOD.BAS:4293-4303). `picMonster_Paint` makes the same substitution in the Library (FRMLIBRA.BAS:106-118). Nowhere else: the portrait beside him in a fight comes off the sheet like anyone else's |
| Automap geometry: fifteen pixels a square laid fourteen apart, wall and door icons half a cell off so one icon serves both squares | **decompiled source** | `mc001C = 15` (FRMAUTOM.BAS:7); `Form_Load` sizes picMaze at `squares * 14 + 1` (113-114); `medDrawSquare` puts bits 0, 2, 4 at `x + 7` and bits 1, 3, 5 at `y - 7` (366-371) |
| Automap: the numbers 2, 4, 6 ... up the left edge and along the bottom | **decompiled source** | `sub2E18` (FRMAUTOM.BAS:503-522), which prints `Str$(i)` for `i = 2 To width Step 2` at `(i - 1) * 14`: the columns only on a form at least full width (508), the rows only on one at least full height (515), and a picture grown to fill a smaller form covers the other set. The port had always shown them - corrected 2026-09-27 |

## More faults in the original, reproduced

**A reproduced fault is only safe while its neutraliser is reproduced too.** Several of the faults
below are recorded as harmless, and each is harmless *because of something else* - which means the
something else is load-bearing and was, until now, unchecked. Three have been pinned:

- **The window sizes a new character is given.** Index 1 is seeded from a table with no entry for
  it; index 14 has an entry and is never seeded. Index 14's zero is never read because the Control
  Panel leaves it out of the Window Control list *and* out of Adjust to Display; index 1's
  meaningless value is never read because there is no window numbered 1 at all. Both exclusions are
  now asserted, not just the fault.
- **Zero and minus one at the two equip routines.** The port tests `!= 0` where `< 1` or `> 0` would
  read more tidily, and that choice is the whole behaviour: Miscellaneous Object carries -1, and a
  tidier test would treat it as unwearable and silently delete the one class of item you may wear
  any number of. Tidying it to `> 0` fails the check, which was confirmed rather than assumed.
- **The `NoToolBar` casing slip** in the Options window, whose neutraliser - case-insensitive INI
  key matching - is asserted above.

The general shape: when the note says "nothing ever notices", the next question is *what* does the
noticing, and whether that thing is itself tested. A fault can be reproduced faithfully and still
change behaviour later, when somebody tidies the unrelated-looking line that was holding it down.

**The morgue told you the raise had succeeded when it had wrecked you.** The city's morgue built up
its report correctly - "Raise Failed!", then "There are complications...", then "One or more items
were unequipped due to lack of stats..." - and then a stray `msg.Text = "Success!"` a few lines
later overwrote the lot before it was ever shown. A player whose character had just lost five from
every stat, fifteen years and a sixth of their hit points was told, in one word, that it went well.

This one is the port's and not the original's, so it is fixed rather than reproduced. It is recorded
here because the complications themselves are easy to think untested when the only way to see them
was to read the save file afterwards.

**The discount on chest experience is taken from whoever stands first in the party.** Disarming a
chest's trap pays experience, `monOpenChest` (MONSMOD.BAS:3508-3509):

```
l0502 = 1 + (Chars(0).GuildsJoined(Chars(0).CurrentGuild).Level / mc00BC) * 3
sub0F11 pv04F2, CLng((abilityChance(pv04F2, m04E0, False) * (Monsters(Fight.M4F24).GuildLevel / 2)) / l0502)
```

with `mc00BC = 999`, so `(level / 999) * 3` is `level / 333` and the divisor is the reciprocal of
the Experience_Awarded_Formula page's GuildLevelFactor. The award goes to `pv04F2`, the character
opening the chest, and the disarm chance is `abilityChance(pv04F2, ...)`, also theirs - but the
guild level that scales it down is **`Chars(0)`**, the first character in the party.

Experience is meant to be worth less to a character who is already powerful. Here the "already
powerful" is read off somebody else. A level 1 character opening a Goblin's chest at a 60% disarm
chance earns 299 experience when they are alone or first in the party, and 150 for the identical
disarm when the party is led by a level 333 character. `Mordor.DataDump chest` prints that table.

It is the same species of fault as the window-size *readers*, which all use character 0: a routine
that means "the current character" and writes `Chars(0)`. (This sentence used to say "writers",
which was the wrong half - the writers disagree with each other, and most of them write to the
whole party.) Reproduced.

**Nothing is earned for a chest that was not trapped.** The award sits inside the
`Fight.ChestTrap > 0` branch, so "Not Trapped" pays nothing at all - only a trap you actually
disarm does. The Experience_Awarded_Formula page gives the chest formula without saying this.

The port had not been awarding chest experience at all; it rolled the disarm and threw the result
away. Now `Rules.ChestExp` computes it and `CombatEngine.GiveExperience` puts it on, which is
`sub0F11` factored out of `AwardSpoils` - the chest needs it after the fight has already ended and
the spoils have been handed out, which is exactly when `monOpenChest` runs.

**The "[maximum 98?]" on the initiative page is a real fork, not an uncertainty.** The community's
Combat_Initiative_Formula gives `Initiative = 50 + ((MonsterDex - CharacterDex) * 5)` and then
`(minimum 5, [maximum 98?])`, with the question mark. `sub0A64` (MULTIMOD.BAS:283-360) is the
routine, and it takes an argument saying which caller it is. Two of the three do the roll, and they
clamp differently:

| caller | argument | clamp |
| --- | --- | --- |
| MULTIMOD.BAS:557, 586 - the monsters' own tick | 0 | no roll at all; every group attacks |
| FRMDUNM.BAS:339, 352 and MAZEMOD.BAS:1471 - you moved | 1 | `If l009E < 5 Then l009E = 5`, and nothing else |
| MAZEMOD.BAS:818 - you changed level | 2 | the same floor, and `If l009E > 98 Then l009E = 98` |

So a monster 10 or more points of Dex ahead attacks 98% of the time when you take the stairs and
100% of the time when you walk. Both are the original, which is why the page could not settle on one
number. `Mordor.DataDump selfcheck` asserts the pair through `CombatEngine.AttackChance`, so a later
tidy-up cannot quietly make the two paths agree.

It is also not an initiative roll. Nothing in `sub0A64` decides who goes first: it decides whether
each of the four monster groups lands a free attack as you move, on a character it draws separately
per group with weights 100, 50, 25, 12, 6 down the party. `CombatEngine.MonsterAttacks` has had all
of this right; what it lacked was the citation, and the port also carried a leftover
`Rules.MonstersFirst` and `CombatEngine.MonstersHaveInitiative` pair that modelled a global
who-goes-first roll the original does not have. Only the combat simulation still called them, so the
simulation had been handing the monsters a free round before every fight. Both are removed.

**Protection from a spell is worth half what the same protection is worth ten seconds later.** The
Protection case of the cast dispatcher writes

```
Chars(l01B6).Ailments(mc0214) = Int(casterSpellLevel(pv01A6, pv01A8) / 2) + 1
```

(MAINMOD2.BAS:1046) - the caster's **spell** level halved - and unlike the resistance case a few
lines above it, that branch does not call `recomputeEffects` afterwards, so the value stands.
`recomputeEffects` writes a different number into the same slot (MAINMOD2.BAS:2805 and 2827):

```
Chars(pv045E).Ailments(mc0214) = Int(Chars(pv045E).GuildsJoined(Chars(pv045E).CurrentGuild).Level / 2) + 1
```

- the **guild** level halved, roughly twice as much - and overwrites the cast's value the next time
anything triggers a recompute: equipping or removing an item, an antimagic square (MAZEMOD.BAS:1749),
a Blackout chest trap (MONSMOD.BAS:3609).

`fn13FE` (MAINMOD2.BAS:2711-2715) turns whichever number is in the slot into defence as
`Log(p) * (Log(p) * 3)`. A Wizard at guild level 26 has spell level `Int(26 / 2 + .5) = 13`, so the
cast leaves 7 and about 11 points of defence, and the next recompute leaves 14 and about 20.

The community's Spell_Damage_Formula page records both halves without connecting them - "For
Protection gained through items: Added Defense = 3 * LN(SL)^2. For Protection gained through Guild
Spells: Added Defense = 3 * LN(Int(SL / 2) + 1)^2", with "Buggy" written against the second - and
the two expressions are these two writes.

Reproduced. `Mordor.DataDump selfcheck` casts Protection on a Wizard 26, asserts the 7, calls
`Effects.Recompute`, and asserts the 14, so it stays a fault that is deliberately carried rather
than one that quietly settles on a single value.

**The help's spell pages disagree with MDATA2 on eight numbers.** Every spell entry in the original
help prints a header - `Base Level: 6  Guilds: Sor, Wiz (18)` and `Required Stats: 18 Int, ...` -
and `Mordor.DataDump spellhelp` reads all 92 of them back against the data the program actually
runs on. Most of it agrees exactly: the guild lists match on all 92 entries, which is the strongest
of the four checks because the list is not stored per spell anywhere - it falls out of each guild's
MDATA1 row - and the bracketed spell levels match on 190 of 191 guild/spell pairs.

Eight do not, and in each case the port follows MDATA2, because MDATA2 is what the program reads:

| the help says | MDATA2 says |
| --- | --- |
| Cause Wounds, Base Level 2 | level 1 |
| Cause Heavy Wounds, Base Level 4 | level 3 |
| Firebolt, Base Level 2 | level 1 |
| Blue Flame, Base Level 4 | level 3 |
| Charm Humanoid needs 16 Int, 16 Wis, 18 Cha | 14 Int, 14 Wis, 14 Cha |
| Charm Undead needs 17 Wis | 15 Wis |
| Piercing Darkness needs 17 Int | 19 Int |
| Abolish Undead: `Guilds: Wiz, Hea`, no bracket on Wiz | the Wizard's rate of 2 makes it spell level 16 |

None of these is a whole-page convention: Firebolt and Blue Flame sit on the Sorcerer's fire page
with four other spells (Flamesheet 5, Pillar of Fire 7, Sphere of Flames 10, Burning Air 12) that
agree with MDATA2 exactly, and each of those spells' other fields agree - Firebolt's stated
`12 Int, 9 Wis` is MDATA2's `req 0,12,9,0,0,0` to the letter. The likeliest explanation is that the
help was written against a slightly earlier data set and these entries were not revisited.

`selfcheck` pins the counts at 4 base levels, 3 stat lines and 1 missing bracket rather than
asserting zero, so the known faults stay documented and any *change* in them is caught.

**Reset files the new window sizes under a character nothing reads them back from.** Four
different conventions decide whose copy of the `M5958` table a window's placement is written to,
and only one of them agrees with how the table is read.

**Every reader uses character 0.** All fourteen forms do, each through its own name - a `Const`
set to zero in nine of them (`m0078`, `m014A`, `m006A`, `m013E`, `m0040`, `m00C4`, `m00A4`,
`m0052`, `m008C`) and a never-assigned `Static ... As Integer` in the other five (`m0054`,
`m0034`, `m0176`, `m00B4`, `m00DE`), which VB starts at zero and nothing moves.

The writers do not agree with each other:

| writer | whose copy | what it is |
|---|---|---|
| `sub1614 -1, ...` (MODULE6.BAS:4370) | **every character** | the shared saver, called by twelve of the thirteen forms' `Form_Unload` |
| FRMAUTOM.BAS:173-176, 571-574 | character 0 | the automap, which never calls `sub1614` and assigns by hand |
| CHARCREA.BAS:315-342, FRMCONTR.BAS:111-182 | character 0 | seeding a new character, and Adjust to Display |
| `sub074E` (MODULE2.BAS:1718) | `gv1A82` | **the Control Panel's Reset** |
| `sub162C` (MODULE6.BAS:4405-4446) | `gv1A82` | a batch save of the five main windows |
| `sub150A` (MODULE6.BAS:3217-3238) | `gv1A86` | the default layout laid out at character creation |

`gv1A82` is not "the character the windows are showing" - that is `gv1A86`. There are two ways to
change character and they move different globals. `sub1536` (MODULE7.BAS:651) sets all three of
`gv1A82`, `gv1A84` and `gv1A86`; `sub178B` (MODULE7.BAS:600) sets only the latter two. Single
clicking a party member's name calls `sub178B`, double clicking calls `sub1536`
(FRMGROUP.BAS:171-179), and the help's Commands page lists the double click as the way to switch.
So `gv1A82` is the last character *fully* switched to, and it lags behind `gv1A86` after a single
click. Two places test for exactly that and resynchronise: `If gv1A82 <> gv1A86 Then sub1536
gv1A86` (FRMGROUP.BAS:32, MODULE6.BAS:3413).

So Reset and `sub162C` file under whoever was last switched to. Playing alone, or with the leader
in focus, that is 0 and nothing is wrong. Otherwise the sizes go to a slot no reader looks at:
`sub074E` sets each form's `Width` and `Height` directly as well, so the windows are the right
size until you reload and the default size afterwards.

Reproduced in `ControlPanelWindow.OnReset`, which writes to the focused character where Adjust to
Display writes to the leader.

**What this entry used to say.** That "every writer of the `M5958` table uses slot 0", citing the
automap's `Form_Unload` as though it were the pattern. It is the single exception: every other
form saves through `sub1614` with -1, to the whole party. The port had inherited the mistake -
`MdiFrame.Save` took one character - so a layout arranged by a party would have been lost the
moment the first slot changed hands. It now takes the party and writes to all of them, with the
automap kept narrow because the original keeps it narrow.


**The quote is opened and never closed whenever a character speaks.** Both places that write the
cry for help build the line the same way:

```
sub085B RTrim(Chars(l0144).Name) + " - " + Chr$(34) + "Help! My hits are " + ...
```

`Chr$(34)` is a double quote and it appears once, so what reaches the Messages window reads

```
Brannoc - "Help! My hits are 5/15!
```

with nothing to close it.

This entry used to call that "the house style of that routine", on the strength of the paralysis
line having the same missing half. Counting `Chr$(34)` per line across the whole decompiled source
says something more precise: **exactly three lines use an odd number of them**, and all three are
the same construction, `RTrim(Name) + " - " + Chr$(34) + speech`.

| where | what is said |
|---|---|
| FRMDUNM.BAS:596 | `"Help! My hits are ...` - `timAilment_Timer`, said while you walk about |
| MODULE4.BAS:3404 | `"Help! My hits are ...` - `sub0E85`, after a group finishes on you |
| MODULE4.BAS:3290 | `"I've been Paralyzed!` - the same routine, on a paralysis |

Every other `Chr$(34)` in the source comes in pairs, wrapping a name or a spell title. So it is not
one routine's habit - it is what the game does every time a character speaks, and there are only
two things a character ever says. Both are reproduced, in `HelpCry.Line` and `HelpCry.Paralysis`.

Paralysis has a second wording that is not speech and carries no quote: the character you are
watching gets `"You have been Paralyzed!"` as a caption in the window instead
(MONSMOD.BAS:3286). That is the neutraliser worth keeping in view - if the caption were ever
built from the speech line, the missing quote would follow it there, where the original never puts
one.

The scan is asserted rather than the conclusion, because a fourth speech line turning up later
would be a fourth reproduction to make and nothing else would notice.


**Casting Levitate also grants See Invisible.** `sub0894` (MODULE6.BAS:2785) walks bits 0 to 5 of
the gear mask and the spell mask together, reading both with the *gear* meanings, and then
re-tests the spell mask with the *spell* meanings - which are at different bit positions. Items
carry their abilities at bits 0-3 (Levitate, Invisible, Protect, See Invisible; MODULE1.BAS:518-523),
spells record theirs at bits 3-6 (`mc0206`=3 Levitate, `mc020C`=4 Invisible, `mc0210`=5 See
Invisible, `mc0214`=6 Protect; MODULE6.BAS:54-60). Three of the four spell bits therefore land
inside the shared loop wearing the wrong name:

| spell writes | the loop reaches | which sets |
|---|---|---|
| Levitate, bit 3 | `Case 3` | **See Invisible** |
| Invisible, bit 4 | `Case 4` | nothing - the case is empty |
| See Invisible, bit 5 | no `Case 5` exists | nothing |
| Protect, bit 6 | past `mc0318 - 1` | nothing |

So exactly one of the four leaks, and because the four explicit tests afterwards are correct the
loop can only ever *add* a state - Levitate still arrives. Casting Feather Essense leaves a
character levitating and seeing invisible, which is not cosmetic: See Invisible is what stops
`fn0E23` printing an invisible monster as "Unseen Monster", so the leak silently names monsters
the caster should not be able to make out.

The same loop is right for gear - Boots of Levitation grant only Levitate - and that half is
asserted alongside the leak, because it is what makes this a clash of two bit vocabularies rather
than a table that is simply wrong. Reproduced in `Effects.Recompute`; both halves are pinned in
`selfcheck`, and removing the fault fails the Levitate row and no other.

**Fate rolls a number it never uses.** `sub0F29` (MODULE6.BAS:1906) begins `l031E = Int(Rnd * 3)`
and then never reads `l031E`. Whatever branch that was meant to pick does not exist: the routine
always rerolls both hits and spell points. What the three branches were for is not something the
source says, so it is not recorded here. The port does the same and draws no such number. That is
not a perfectly neutral choice - the discarded `Rnd` still advances VB's generator - but the port
uses .NET's generator rather than VB's, so the two sequences never lined up to begin with.

What the routine *does* do is worth stating alongside, because the arithmetic has a consequence
the name hides:

```
M4EAC = Int(Rnd * M4EB4) + 1            ' current hits         = 1..max
M5830 = Int(Rnd * fn138E(pv031C)) + 1   ' current spell points = 1..max
```

The `+ 1` is added *after* the `Int`, so both draws are 1 to max and neither can be 0. **Fate
cannot kill.** It is also a reroll rather than damage, drawn uniformly across the whole pool, so
on a healthy character it is close to halving and on a nearly dead one it is a rescue - 39 of the
40 outcomes on a 40-hit character beat being on 1. Reached two ways, chest trap 6
(MODULE4.BAS:3553, inside a `Select Case l0528 - 1` so the case number is one below the trap
number) and spell effect 19; both are the same routine, and `selfcheck` asserts them separately
so they cannot drift apart. Dropping the `+ 1` fails seven of those checks and leaves every
spell-point check green, which is what says the two pools are pinned independently.

Worth reading against [the drain fault below](#a-drain-attack-can-never-touch-strength), which is
the same `Int(Rnd * n) + 1` shape and also draws 1 to n. The difference is not the arithmetic - it
is what the consumer expects. Fate's caller wants 1 to max and gets it. The drain's `Select Case`
wants 0 to 5 for the stats and 6 for the hits, so the same 1-to-n draw silently loses index 0.
The `+ 1` is a floor in one and an off-by-one in the other, and nothing about the expression says
which.


**A drain attack can never touch Strength.** `sub0E85` picks the stat with
`l04A8 = Int(Rnd * mc04AC + 1)`, where `mc04AC` is 6. The `+ 1` is *inside* the `Int`, so the draw
is 1 to 6, never 0. The `Select Case` that follows reads

```
Case False To mc04AC - 1        ' Case 0 To 5  - drain that stat
Case mc04AC                     ' Case 6       - take 2 off maximum hits instead
```

so the reachable stat indices are 1 to 5 - Intelligence, Wisdom, Constitution, Charisma,
Dexterity - and index 0, Strength, is unreachable. A wraith can drain everything about you except
the one stat that makes you hit harder.

**Correction.** This entry used to say the `+ 1` was meant to be *outside* the `Int`, "which would
have given 0 to 5". That is wrong, and the arithmetic is easy to check: `Int(Rnd * 6 + 1)` and
`Int(Rnd * 6) + 1` are both 1 to 6, because `Rnd` never reaches 1. Moving the `+ 1` changes nothing
at all. What reaches Strength is dropping it - `Int(Rnd * 6)` is 0 to 5, which lines up exactly with
the `Case False To mc04AC - 1` arm and leaves `Case mc04AC` dead.

So the `+ 1` is the whole of the fault, wherever it sits. Whether the author meant to write it is
not something the source says, and the old sentence guessed. Confirmed by removing it: Strength is
then drained about a sixth of the time and the check below fails.

Reproduced in `CombatEngine.GroupAttacks` (MONSMOD.BAS:3142-3168). `Mordor.DataDump drain` reports
who loses what over three thousand rounds - nothing else in a monster's turn moves a stat, so this
one can be watched from outside rather than needing the draw pulled out.

**Acid never touches your first companion, and one attack in five hits nobody.** The spit attack
draws its target with `l0474 = Int(Rnd * (mc01A0 + 1))`, which is `Int(Rnd * 5)` - 0 to 4, where
0 means the character and 1 to 4 are read straight back as companion slot indices. Companion
slot 0, the first one you charmed, is therefore unreachable: the value that would select it
already means "the character".

The other end is worse. `M58CB(4)` declares **five** slots, 0 to 4, but every loop in the game
that fills or clears them runs `0 To mc00E0 - 1`, which is 0 to 3. Slot 4 is never written, so
its monster index keeps the zero it was born with rather than the `-1` that marks a slot empty -
and the emptiness test is `If M58CB(l0474).M4ED4 = -1`. Slot 4 passes the test, the loop accepts
it, and the acid is subtracted from `M58CB(4).M4EAC`: a companion who does not exist. A fifth of
every spit attack lands on a ghost.

Both are reproduced in `CombatEngine.GroupAttacks` (MONSMOD.BAS:2769-2831), and the draw itself is
`CombatEngine.AcidTarget`, pulled out so it can be asserted. Over 60,000 draws each of 0 to 4 takes
a fifth, and applying the acid code's own rule to those draws - `t != 0` and `t < comps.Count`
selects `comps[t]` - gives a reachable set of **1, 2, 3**. Slot 0 is not in it.

It had to be extracted to be seen. Measured through a whole combat round the fault is invisible:
the monster swings as well as spits, melee reaches every companion, and slot 0 takes damage like
the rest. A rule buried inside a larger routine can only be asserted through everything else that
routine does, which is usually enough to hide it.

**An item handed over is re-read against the wrong item.** `pnlItem_DragDrop` (FRMEXCHA.BAS:199)
tests with `fn12F0(char, Pack(slot).ItemRef)` and then assigns with
`fn12F0(char, Items(Pack(slot).ItemRef).LevelScale)`. `fn12F0`'s second argument indexes
`gv1426`, so it is an item *reference*. The test passes one; the assignment passes that item's
**LevelScale** - a difficulty number - so what gets stored is how well the receiver could read
whichever item happens to carry that reference.

**Corrected 2026-09-22.** This entry used to say the port did not reproduce it, "because the two
readings disagree, so there is no single behaviour to be faithful to". That reasoning does not
hold. The expression is deterministic, and the only question was whether the numbers it produces
land anywhere: every `LevelScale` in MDATA3 is between 0 and 80, and every one of them matches a
real item id, so the stray read is always in range and always lands on a particular item. Nothing
about it is ambiguous. It is well defined and merely wrong, which is the ordinary case for every
other fault in this file.

It is now reproduced in `Loot.OnReceived`. For a level 12 Nomad it changes the answer for 132 of
the 365 items - hand over a Lethe Dagger and the receiver's knowledge of it is set from whatever
item id 9 turns out to be.

Worth noting why the measurement had to be done twice. Item ids are not record positions in
MDATA3: the ids are a shuffle of 0 to 365, and 356 of the 366 records carry an id different from
their own position. A first pass read `items[LevelScale]` as a position and produced a different
set of victims. `fn12F0` indexes by reference, so it is the id that counts.


**Zero and minus one mean opposite things to the two equip routines.** A class maximum of 0
stops `sub0F74` dead (`If gv15C6(l02F8).M54F1 = 0 Then Exit Sub`, MAINMOD2.BAS:1724) but reads to
`fn11B6` as "no cap at all" (`If gv15C6(l0162).M54F1 > False`, MAINMOD2.BAS:576). For the eight
consumable classes that is harmless, because the toggle refuses first and `fn11B6` is never
reached. But Miscellaneous Object carries **-1**, and -1 is not 0: the toggle lets it through, and
then `fn11B6` declines to cap it. So the seven Miscellaneous Items - Medusa's Head among them -
are the only things in the game you may wear any number of at once. The port reproduces this, and
tests `!= 0` rather than the tidier `< 1` precisely so it does.

MDATA1 holds twenty-four wear classes and three maxima: 1 for the fifteen you wear one of, 0 for
the eight consumables, and -1 for Miscellaneous Object alone. `Mordor.DataDump classdump` prints
that table, which nothing else did. Changing the test to `> 0` fails the checks, and six of the
seven Miscellaneous Items going onto one character at once is what proves the fault is live rather
than merely written down.

The sentence above used to say "the sixteen classes you wear", in this file and in `Character.cs`.
It is fifteen. Nothing had counted them.

**Neither shelf ever reaches the fifteenth floor.** `mdrReplenishStore` reads the deepest level
anyone has reached and then caps it:

```
Get #gv1B58, 2, l02BC
If  l02BC >= mc0118 - 1 Then
    l02BC = mc0118 - 1
```

`mc0118` is 15, so the deepest floor the replenisher ever draws from is 14. The same two lines
appear again in `mdrReplenishCStore` (MAINMOD.BAS:1308-1310), so the Confinement's creature pens
have the identical cap - this entry used to mention only the store.

The dungeon has fifteen floors, and the three other uses of `mc0118` in that file are all loop
bounds running `1 To mc0118` (MAINMOD.BAS:513, 825, 832), so 15 is the count of floors rather than
the last index and both caps are one short. That reads as an off-by-one rather than a decision.

It bites all the way through, because the draw takes everything with `Floor <= the chosen floor`:
22 items exist only on floor 15 and 36 creatures are found no shallower, and none of them can be
stocked. Run either replenisher six thousand times with the deepest level set to the true bottom
and floor 14 turns up in both while 15 never does. So anything found only on the last floor
reaches the store's shelf exactly one way: a player carries one up and sells it, and the pens have
no equivalent - a floor 15 creature can only ever be charmed.

Reproduced in `StoreStock.Replenish` and `Confinement.Replenish`, both now reading the same named
`StoreStock.DeepestStocked`; the Confinement had the 14 written out twice as a bare literal.

Two items sit at floor **16**, past the bottom of the dungeon - Sword of Flames and the Lamurian
Crown - alongside the ten monsters with `LevelFound` 16. Neither set is orphaned. The item drop
band comes from the monster's own `ItemDropLevel` rather than from the floor you are standing on,
and seven monsters carry 16, so those two are obtainable from a kill even though no shop will ever
hold one. That is the same shape as the level 50 spells, which no guild admits and four items
carry.

**"Item isn't equipped!" is reachable, and this entry used to say it was not.** The General
Store's uncurse (FRMCTYS.BAS:292-302 - the Store, not the Seer) asks two questions and they read
two different fields:

```
If slot.M572A = False Then  "Item is not cursed!"   : Exit Sub    ' the slot's curse flag
If slot.M577E = 0      Then "Item isn't equipped!"  : Exit Sub    ' the slot's equipped bit
```

This is the only place in the game that tests the slot flag without also testing equipped - the
other six reads all pair them - which is what makes both refusals visible and both of them odd.

The entry previously claimed the second branch was dead, on the grounds that the slot flag "is
only ever set while the item is on". That is true of `sub0F74` and of nothing else. Three other
routines write it, and two of them do not equip anything:

| writer | what it is | equips? |
|---|---|---|
| MODULE6.BAS:1786 | `sub0F74`, equipping: copies the item's flag on | yes |
| MODULE6.BAS:1821 | `sub0F74`, equipping something your alignment opposes | yes |
| MODULE6.BAS:1745 | `sub0F74`, unequipping: clears it | yes |
| **MODULE4.BAS:3825** | **`sub0F01`, chest treasure landing in a pack slot** | **no** |
| **FRMCTYG.BAS:745** | **`modAwardCrest`, the Guildmaster's Crest** | **no** |
| FRMEDITC.BAS:224 | the character editor | no |

MODULE4 and FRMCTYG contain no assignment to `M577E` at all, only tests. So chest treasure is
flagged cursed the moment it lands, *before* anything tries to put it on - and what tries is
`sub0F74 l0526, l051A` at MODULE4.BAS:3846, a positive slot index, which is an ordinary equip
subject to the ordinary refusals. Too weak, wrong guild, not wearable: the item stays in the pack,
still flagged.

That is not a corner case. 43 of MDATA3's 365 items are cursed and every one of them is findable;
a fresh level 1 character is refused **38** of them by the equip guards. So the ordinary
experience of a new character who opens a chest and gets a Bone Dagger is to carry it to the store
and be told it is not equipped - which is true, and useless, and the panel offers nothing else.

The crest route is real in the code and dead in the data: 0 of the 12 crests are cursed.

**What the port had.** No slot flag at all - cursedness was derived from the item definition plus
a `CurseLifted` bit - and the uncurse computed `flaggedCursed = slot.Equipped && !CanUnequip(...)`,
which makes the second test unreachable *by construction*. The branch was kept and commented as
faithfully dead. It was not dead; the port had removed the only way to reach it. `InventorySlot`
now carries `SlotCursed`, maintained exactly where the original maintains `M572A`, and
`Uncursing.Refusal` is the two questions in order.

**Two smaller things this turned up.**

The old comment said FRMCTYS.BAS:309 "only clears the slot curse when the item is not inherently
cursed, so an alignment curse is gone for good while a cursed sword sticks again". It does not.
The line above it forces the item off with `sub0F74` and a negative slot index, and that routine
clears the flag unconditionally on its way out (MODULE6.BAS:1745). By the time the test at 310
runs there is nothing left for it to clear. Both kinds of curse come off, and both come back when
you put the item on again. The test is dead but harmless.

And `mdrcharCheckItems` (`fn11E2`, MODULE6.BAS:658-680) calls `sub0F74` with a *positive* index
too, so a cursed item cannot be taken off by it either - but `l018E = True` is set before the call
and the "can no longer use" line is printed regardless. The game tells you an item has come off
and leaves it on you. Reproduced in `GearCheck`.

**The pens can hold more than nine of an aligned creature.** `mdrReplenishCStore` checks the cap
with `If gv162E(l028A).M5557(l0288) < mc0298` and then increments
`gv162E(l028A).M5557(gv14F6(l028A).M4EBF)` (MAINMOD.BAS:1372-1373). On that path `l0288` has not
been assigned, so it is zero: the test reads the **unaligned** count and the increment lands on the
creature's **own** alignment. A Good creature can therefore be penned past nine indefinitely, as
long as no unaligned ones of that kind are there - which, since a creature is always penned at its
own alignment, is usually the case.

Asserted as an invariant rather than as a story: over a long run of restocks, no creature's
**unaligned** shelf ever passes nine, because that is the count the test reads, while an aligned one
reaches twenty-four. Correcting the bug - testing the creature's own alignment, as the line plainly
meant to - caps everything at nine and fails the check, which was run rather than assumed.

The first shape of that test was wrong in a way worth recording. I seeded nine unaligned and
expected the creature to stop being penned; it was penned 390 times more. The pens release as well
as stock - forty percent of passes - so the unaligned shelf drains and unblocks itself. The fault is
not "a full unaligned shelf blocks you", it is "only the unaligned shelf is ever tested", and only
the second survives contact with the release path.

`mdrReplenishStore`'s copy of the same loop assigns its alignment index before testing and does
not have this, so the two routines - otherwise line-for-line the same - behave differently.
Reproduced in `Confinement.PenOne`.

**A new character is given a size for a window that does not exist, and none for one that does.**
`CHARCREA.BAS:315-342` seeds `M5958` for window indices 0, 1, 10, 12, 11, 5, 16, 15, 19, 2, 3, 17,
13 and 18. The table of defaults it reads from (`sub03A2`, MODULE1.BAS:606-648) has no entry for
index 1 and does have one for index 14, so a new character is written a zero size for 14 and a
meaningless one for 1. Nothing ever notices: 14 is the one window the Control Panel leaves out of
both its list and Adjust to Display (FRMCONTR.BAS:97, 216), so its size is never read.
Reproduced in `WindowLayout.SeededByCharacterCreation`.

## Companions - kicking one out, and attacking one

`fn0B6C` (MODULE4.BAS:519-600) is the whole of it, and both the Kick Out button and the right-click
Attack go through it. The difference is one flag.

**A companion that joined of its own free will never turns.** The routine leaves at once when the
binding is at or below zero and the call is not forced, so only a bound or charmed companion is
ever a risk - which is exactly what the warning on the Kick Out box says.

**The three chances read the creature's alignment, not yours.** Two steps away from your own always
turns it; an Evil one turns three times in four; a Neutral one about one time in seven; and a Good
companion of a Good master has no clause that can fire at all. Counted over twenty thousand rolls
with a Good master, the port gives 0%, 15.3% and 100% for Good, Neutral and Evil - the last being
certain because the alignment gap has already decided it before the 75% is reached
(`Mordor.DataDump ... companionpage`).

**A fight already under way can veto the whole thing.** If the monsters on the square are two
alignment steps from the companion, it "refuses to join the current monsters!" and nothing happens -
it does not turn and it does not leave, even when you attacked it.

**With four groups already present, nothing can turn.** The routine looks for an empty one of four
slots and gives up quietly when there is none, so the companion stays with you and no message is
printed.

**Kicking one out that does not turn says nothing at all.** `cmdCompanion_Click` clears the slot and
repaints. The port briefly had it announce a farewell; that was invented and is gone.

### What the Companions page says

`sub0B82` (MAINMOD2.BAS:4704-4745 - this file cited MONSMOD:606-640 until 2026-09-22, which is
where `sub0C00` begins, so the wrong citation landed inside a real routine two lines along and
read as plausible) writes each row as the slot number, then the creature's name in
brackets when the companion has none of its own or its own name in quotes, and the hits separated
by a **backslash** - `12\15`, not `12/15`. Reproduced.

### The Companions page is the form's: four portraits (2026-10-24)

The user: "for the companions, is that tab faithful? i thought normally when you have a companion you also see its
portrait". It was not. The port had a list of rows and its own Kick Out, Attack, Name and Info buttons where the
original draws a picture per companion. The page is now frmCharStat3's own (MORDOR.EXE RCDATA 10, the rectangles in
`ServiceForms.Companions` and held to the EXE by `tools/check.sh`), drawn by `CompanionsPage` in town and below:

- **Where.** On elaTabFrame(2): lblCarryInfo 90,30 3330x210; cmdCompanion(0) designed at 3495,30 270x255, and
  Form_Load loads 1-3 on it and puts 2, 1 and 0 each a button's width left of the next (FRMCHAR3.BAS:384-394), so the
  buttons "1" to "4" run 2685-3765 along the top right. elaElastic 60,270 3705x2205 holds the slots: picCompanion(0)
  60,45, (1) 2910,300, (2) 60,1125, (3) 2910,1395, all 750 square (50 pixels); lblCompName(0..3) 885,60 / 840,600 /
  885,1140 / 840,1680, 1995x225, 8.25 plain; lblCompHits(0..3) 900,315 795 wide / 2025,855 810 / 900,1395 810 /
  2055,1935 780, 225 tall, the form's bold. The left-hand slots' labels stand right of the picture, left-aligned; the
  right-hand slots' (1 and 3) stand left of it, right-justified (Alignment 1). In pixels on the page: pictures at
  8,21 / 198,38 / 8,93 / 198,111.
- **What.** `mdrcharShowCompanions` (MAINMOD2.BAS:4709-4760): a filled slot's picture is repainted and shown, its
  name "n. (Creature)" or "n. \"Name\"", its hits "h\m", its button enabled; an empty slot's picture and labels are
  hidden and its button disabled; with none at all lblCompName(0) reads "No Companions".
- **The picture.** `picCompanion_Paint` (FRMCHAR3.BAS:801-820): the creature's 60-pixel cell of the monster sheet,
  column `(PicId - 1) Mod 10`, row `Int((PicId - 1) / 10)` (`MonsterPortrait.Of`), StretchBlt into the 50-pixel box,
  or with No StretchBlt (`gv1A66.M5F04`) BitBlt at its own size at (50/2) - (60/2), five pixels up and left, so the
  middle 50 of the cell show. Both SRCCOPY. The stretch is drawn with no smoothing, as the Library's picture and the
  rest of the port's bitmaps are; GDI's default stretch mode (BLACKONWHITE) ANDs the rows and columns it drops, where
  nearest-neighbour simply drops them - on a 60-to-50 shrink that can differ by a pixel's shade along a dark edge.
  The box has the port's picture-box face (the original's BackColor is &HC0C0C0), which the cell covers either way.
- **What the controls do.** A picture is dragged with the button down (picCompanion_MouseMove, 795-799) and takes a
  companion dropped on it (picCompanion_DragDrop, 763-779) - the same `"COMP"` payload, so the Confinement's Comp.
  boxes still take it; a double-click is Info (picCompanion_DblClick, 744-759); a right press is Attack in the
  dungeon (picCompanion_MouseDown, 783-793; in town it asks for a dungeon level and does nothing); a click on the
  name is Name (lblCompName_Click, 478-488, not in a fight); button n is Kick Out for slot n (cmdCompanion_Click,
  256-281, the town's refusal included). The port's Kick Out, Attack, Name and Info buttons are gone with the list.
- **What it no longer shows.** The dungeon's list had carried each companion's binding ("Completely bound",
  `Binding.Describe`, fn0D92). lblCompHits holds the hits alone; the wording is on the companion's Info page
  (`CompanionInfo`, a double-click away), as in the original.
- **The Good refusal.** picCompanion_MouseDown (FRMCHAR3.BAS:781-787) says "It is against your ways to attack a
  Good companion." and attacks nothing when the character's and the companion's alignments both equal `m02BE`. That
  is a `Static ... As Integer` never assigned, and VB starts every unassigned variable at 0; alignment 0 is Good on
  both records (the character's, and the companion's, which is the foe's 0-2 copied over, MAINMOD2.BAS:392) - which
  is what the line itself says. The port refuses a Good character's Good companion (`ItemAlignment.Good` on its
  record) with that line; anyone else's right-click attacks as before.
- **A larger window** (a GUI deviation, as the rest of Objects & Spells): the original zooms the form; the port keeps
  every control at its size and moves the right-hand slots and the buttons right and the lower slots down into the
  room (`CompanionsPage.Grow`). At the form's size nothing moves.

Mordor.UiCheck lays the page out headless and checks every rectangle, one picture per filled slot and none for the
empty one, the cell's pixels against the sheet's at its column and row, the stretched and the centred drawing, the
drag, the drop, the double-click, the right press, the name click, the buttons, the larger page and "No Companions".

Its first line is the one worth knowing: `If pv06CE <> gv1A82 Then Exit Sub`. The page redraws for
the lagging index alone, so a single click on a party name - which moves `gv1A84` and `gv1A86` and
leaves `gv1A82` behind - does not repaint it at all. That is the concrete mechanism behind the
staleness the help describes, and it is why `gv1A82` is read almost nowhere except the companion
displays. See
[the current-character entry](#one-current-character-where-the-original-keeps-three).

**A companion has no name until you give it one, and is never called by its breed.**
`FRMCITYC.BAS:123` sets `M548B = ""` when one is bought, and every message the game prints about a
companion - the combat log, the carried-body line, the morgue's Raise button, the Kick Out
question, the Name prompt, "Left!", "has turned on you!" - goes through `fn128B`, which answers
"Companion #N" or the name in quotes. Not once does it use the creature's own name. The port was
naming a joining companion after its breed, which made the bracketed form on the Companions page
unreachable and the Name button pointless; one namer now serves the lot
(`Character.CompanionName`).

The wording around it is the original's too. A message about the leader's own companion names it on
its own; anybody else's is prefixed with their name in quotes and a colon (MONSMOD.BAS:649-652).
The Name prompt is titled `"Name " + fn128B(...)` and asks "New Name?" (FRMCHAR3.BAS:477).

**Companions leave of their own accord.** `sub0C00` (MODULE4.BAS:604-660) runs once a second for
every character, between paralysis wearing off and the old-age check, and moves every companion's
binding one notch towards zero on a roll against `Rnd * 200`. Positive bindings count down and
negative ones count up, and the line depends on which side of zero it lands: "has broken a level of
binding!" at or above, "is becoming restless!" below. Then, on a second roll against `fn0D78` - or
outright when the binding hits zero - it runs the same `fn0B6C` a Kick Out runs. If that turns the
companion it becomes a monster; if not, it goes, and the game says "Left!".

How fast it drifts depends on which kind it is. A **content** companion - one that joined freely,
so a negative binding - drifts on how hurt it is and nothing else: nothing at full health, fifteen
at death's door. A **bound** one drifts on the creature's own guild level against the master's,
`(guildLevel / ln 3) * 2 - (topGuildLevel / ln 12) * |binding / 2|`, so a powerful companion on a
weak master slips fast. Each has a floor - half a point content, a whole point bound - so nothing
is ever held completely still, and a binding of `gc1D78` (bound for life) is not touched at all.

Measured over four hundred runs each with a Dwarven Lord on a level-10 master
(`Mordor.DataDump ... companionpage`), the median wait before it leaves, in rounds of one second:

| binding | drift | rounds |
|---|---|---|
| -10 | 0.5 | 827 |
| -4 | 0.5 | 14 |
| -1 | 0.5 | 8 |
| 2 | 61.5 | 4 |
| 6 | 53.5 | 13 |
| 10 | 45.4 | 27 |
| 100 (for life) | - | never |

A companion that joined of its own accord and is unhurt therefore sticks around for a quarter of an
hour, while a creature you charmed or paid for is gone in under a minute unless you pay again. That
is what the help means by "bound companions are likely to turn on their master": the binding runs
down fast, and each notch is another chance for `fn0B6C` to round on you.

**A companion cannot have more hits than its maximum.** `FRMCITYC.BAS:128-129` sets both from the
creature's own `M4EAC`, so they start equal and only fall. The port was carrying a monster's rolled
hits across when one joined, which can be above that figure, and the page read `4\3`. Clamped.

**Companions live in four fixed slots there - and, since 2026-09-26, here too.** Clearing one leaves a hole in the
original and the slots above it keep their numbers. The port kept a list, and had to bring the index of a body
being carried down when a companion below it was removed; now the carried companion is a slot number
(`M5943`), as in the original, and nothing moves (`CompanionSlots`).

**The automap's double-click is a toggle, not a reset.** The help calls it "Either Resets or
Resizes the Automap" and `picMaze_DblClick` (FRMAUTOM.BAS:585-598) says which: at the full default
size it drops to that size over **2.25**, and at anything else it goes back up. That is the same
divisor character creation uses on window 18, so a new character's automap opens at the small size
and the double-click is how you get the big one - the two halves of the arithmetic explain each
other. The right button calls the level's own redraw, which is the help's "Redraw/Refresh Level".

**The automap window has its own floor.** `Form_Resize` refuses anything below a quarter of the
default in either direction, which is the only floor it needs there: the map is drawn from the cell
size and the floor's dimensions and re-laid on every resize, and the chrome around it is one
scrollbar and a label. This port has a slider and two buttons (the legend is gone), so its automap child gets
a content floor like every other window (see "A window is never given less room than its contents need"). Its map is never scaled: see
modAdjustMap below (corrected 2026-09-27), and the next section.

### The automap is not scaled (re-examined 2026-09-28)

A screenshot of the original seemed to show the Automap grown with its window: the whole 30x30
level filling a client area of about 590x620, squares about 19 pixels apart instead of 14, the
numbers spaced to match. That would mean the map scales, and the port does not scale it. The
source was read again for any way it could, and there is none:

- **Every blit is BitBlt.** All the drawing in FRMAUTOM - medDrawSquare (FRMAUTOM.BAS:367-381),
  medDrawCursor (304, 316, 325-326), timBlinker (613-622), "CLEARSQ" (226) - goes through
  `extfn182D`, which MODULE8.BAS:7 declares as GDI `BitBlt`: a source rectangle copied 1:1, with no
  destination size to stretch to. `StretchBlt` (`extfn01C4`, MODULE1.BAS:37) and `StretchDIBits`
  (`extfn1844`, MODULE8.BAS:27) are never called by the form.
- **Every coordinate is fixed.** Squares at `(n - 1) * (mc001C - 1)`, icons `mc001C` square, the
  cursor `mc001C - 4`; the boxes medPlotMaze and "DRAW" fill (232, 272, 421) are the same fixed
  pixels. Nothing reads the form's or the picture's size to work out a scale.
- **picMaze's size is set once**, in Form_Load, from the level's squares (113-114), and at or above
  full size modAdjustMap puts picPicture back to exactly that size plus its border (474, 491).
- **The sub2E18 numbers** are printed at `(i - 1) * 14` and `i * 14` (510, 518), in the form's own
  font - fixed too.
- **No generic scaling.** The routines that scale a form's controls by its size, sub0760
  (MAINMOD.BAS:1750-1850) and sub07C5 (MAINMOD.BAS:1925-1982), are called by the city, dungeon and service forms
  and by sub07ED, never by frmAutoMap; its Form_Resize is the minimum-size clamp, sub2E18 and
  modAdjustMap and nothing else (151-170).
- **No StretchBlt option.** The Options "No StretchBlt" (gv1A66.M5F04) is read by the portrait and
  maze routines (MONSMOD.BAS:4093-4102 and others, MAZEMOD.BAS:1481) and not once in FRMAUTOM;
  there is nothing for it to switch.
- **The help agrees.** Its AutoMap page says you can resize the map "by dragging the corners or
  double-clicking on the map itself. When the Automap is smaller than the base size, the map will
  center itself in the window" - and nothing about it growing.

What the screenshot shows is the full-size form seen through Windows' own display scaling. The
original runs here under OTVDM, and `otvdmw.exe`'s manifest declares no DPI awareness, so on the
user's 125% display Windows bitmap-stretches the whole window: 14 pixels become 17.5, the 458x493
form about 572x616, and the slider row, the numbers and the text all grow with it. (The user's
earlier automap screenshot was already known to be 1.25x - 594x690 for a 475x553 window, above.)
A check anyone can make on such a picture: column numbers 2 and 30 are 28 x 14 = 392 pixels apart
in the game, so 490 on a 125% screen, and the caption bar's text is as much bigger as the map.

So the port keeps the map at 1:1 - a bigger window shows more grey round it, as the original's
does - and selfcheck now asserts the source facts above, so a later reading cannot put scaling
back without first contradicting them.

Everything above still stands as a statement about the original. Since 2026-10-14 the port does
enlarge the map on a big enough window, at the user's request - a GUI deviation, recorded under
"Deviation, at the user's request: the automap grows with its window" below.

**Party Options has two refusals for one rule.** Press the Options button on the Party
Information window and it says "Cannot set default options while in Combat!" (FRMGROUP.BAS:41);
use the keyboard command for the same thing and it says "Cannot set default options while monsters
are in the area!" (MAINMOD2.BAS:4059). The test either way is `fn0EEF()`, so the second sentence
is the accurate one and the first is what the button happens to say. Both are here, on the paths
that say them.

**No teleporter square in the shipped dungeon is fatal.** `mdrcharTeleport` has two ways to kill
you (MAINMOD2.BAS:5839-5893): a destination level of zero or less drops the whole party "hundreds
of feet above the city and fallen to your death", and a destination square that is rock kills
whoever lands in it. Neither can be reached by walking onto a teleporter.

All 86 across the fifteen levels were checked against MDATA11. Forty-eight name a square, and
every one of those is on a level between 1 and 15 and is not rock; twenty-seven of them change
level. The other thirty-eight carry no destination at all and are re-drawn on the current level
until the square is not rock, so they cannot be fatal either. `Mordor.DataDump teleports` lists
every one and what is at the other end.

So the two deaths are reachable only from the Teleport and Displacement spells, which ask you for
the destination and will take a bad one.

**How strong that is.** Rock is 7.8% of the dungeon - 1,053 of 13,500 squares - so forty-eight
destinations placed at random would all miss it about one time in fifty. That is evidence the
destinations were chosen rather than proof of it, and the check records the percentage so the
strength of the claim is visible rather than assumed. I had guessed rock was most of the map
before measuring it, which would have made the same result nearly conclusive; it is not.

**Two of the four gold messages cannot happen.** `monGetGold` (`fn0DBF`, MONSMOD.BAS:2121-2178)
sums everybody's recovered gold into the found gold and *then* wraps the announcement in
`If l039C > 0`, testing `l039C` again inside it:

```
l039C = l039C + l03A4                       ' found + everybody's recovered
If l039C > 0 Then
    If Parties(gv1A86).Recovered = False Then
        If l039C > 0 Then  "Your share is N Gold!"
        Else               "You didn't find any Gold!"                        ' dead
    Else
        If l039C Then      "Your share is N and you recovered M Gold!"
        Else               "You didn't find any gold, but M was recovered!"   ' dead
```

Both else-branches need `l039C` falsy inside a block that already requires it positive. The second
is dead twice over, which this entry did not say: reaching it needs the selected character's
recovered gold to be nonzero, and that figure is already part of `l039C`.

Only the two reachable wordings are implemented, and that is why the other two are missing rather
than forgotten. `GoldShare.Line` is the whole decision, and `selfcheck` walks every combination of
found and recovered and confirms nothing else ever comes out.

**Which figure each of the two live lines quotes.** The share is the whole pot - found plus
*everybody's* recovered gold - divided by the party. But whether the sentence mentions recovery at
all, and the number it names, come from `gv1E66(gv1A86).M68C8`: the **selected character's own**
recovered gold (MONSMOD.BAS:2155 and 2163). Somebody else being robbed and getting it back is in
your share and not in your sentence.

The port had collapsed that to one party-wide figure, so it told a character who had never been
touched that they had recovered gold, and quoted the party's total as theirs. `GoldRecovered` is
now a per-character tally, as `M68C8` is, cleared after the payout as MONSMOD.BAS:2173-2175 clears
it.

**One thing not reproduced.** `lblCharacterHit` is made visible *before* the `If`, and the caption
is assigned after it, from a `Dim`'d local string that is empty when nothing was printed. So a
fight that yields no gold shows a blank label and still waits half a second (`sub07E2 .5#`) before
hiding it. This port's combat is a log of lines rather than a label with a timer, and it has
nowhere to put a blank pause; the stall is absent. Recorded here so it is a known gap rather than
an oversight. (Since 2026-09-27 the printed line is lblCharacterHit's, for half a second, and not a
Messages line - see that day's queue entry. The blank caption is still not shown.)

**A stray corroboration.** The dead branch at MONSMOD.BAS:2165 reads `gv1E66(gv1A82).M68C8` where
the live one two lines above reads `gv1E66(gv1A86)`. The two are mixed up elsewhere as well - see
[the window-size entry](#reset-files-the-new-window-sizes-under-a-character-nothing-reads-them-back-from) -
and here it costs nothing only because the line can never print.

**"We don't accept Elfs!"** The guild builds the plural of a race by sticking an s on the end of
its name (FRMCTYG.BAS:790), which is right for Humans and Gnomes and wrong for Elves and Dwarves.
The port says Elfs and Dwarfs too.

**Detect Rock does not agree with itself.** The spell answers "The square ahead is Rock." when
it is and "The ahead square is not Rock." when it is not (MAINMOD2.BAS:1237 and 1257). Both are
written out in full, twenty lines apart, so neither is a slip for the other in any way this port
can settle, and both are here.

**Perceiving ahead never asks whether you already knew.** `mdrmazPerceiveSquare` exits
when the bit is already on your automap, so it tells you about a square once. `mdrmazPerceiveAhead`
has no such test: it rolls against the maze every time the view is redrawn, so a pit you are
standing in front of is announced again on every turn that leaves you facing it, for as long as
the rolls keep coming up.

One of the eight branches has a suppression test and it is not a fix for that. Quicksand asks
whether you are standing in quicksand *yourself* - not whether you have been told before - and the
bit is written into the automap on the line above, before the question is put:

```
l0170 = fn0418(gc1D36, l0170, True)             ' MAZEMOD.BAS:517, onto the map
If  fn0380(gc1D36, l0172) = False Then          ' 518, l0172 is the square you are on
```

So a pit ahead still speaks while you are stuck in quicksand, and the quicksand ahead is mapped
whether or not it is mentioned. All three are asserted.

**And it says "rotator" to one player and "Rotator" to two.** The eight features' lines are written
out twice, once for a lone character and once for a party (MAZEMOD.BAS:515-588), and two of the
eight pairs disagree with themselves: "a rotator ahead of you" against "a Rotator ahead of the
party", and "an Extinguish square" against "an Extinguish Square". Both spellings are in the
source eight lines apart, and both are here.

`selfcheck` reads all sixteen strings out of `MODULE3.BAS` and compares them with what the port
would say, rather than holding a copy of them. Tidying either half of either pair fails the check
and names the line.

**Ten of the twenty-three automap icons arrive only by perception.** The `PICS` sheet carries an
icon for every one of the twenty-three square flags, and `medDrawSquare` will draw any of them -
but walking onto a square only ever writes six edge bits and seven features into the automap file
(MAZEMOD.BAS:1610). Everything else reaches the map through `sub087C`, the perception routine,
which writes the bit it worked out straight into the file (MAZEMOD.BAS:634-637). That is both how
the game knows not to tell you twice and how the icon appears at all.

There are nine `sub087C` call sites in MAZEMOD and one more in MAINMOD2, and four of the nine are
consecutive: MAZEMOD.BAS:1709-1723 perceives each of the four **FACE** squares and sets the
character's facing straight afterwards. So the green arrows at cells 6-9 are written exactly as
the rotator and the anti-magic square are.

This entry used to say those four "have no writer at all", and `AutomapPics` said it too. That was
wrong on both counts: they are not a picture of which way you are facing, they are forced-facing
*squares*, and perception both notices them and files them. The port's own perception pass has
been writing them the whole time, so the code and the note disagreed.

Two of the ten skip the perception roll entirely. A teleporter is always noticed - MAZEMOD.BAS:631
reads `Or p0188 = gc1D32` before the chance is rolled - and an anti-magic square marks itself the
moment you try to cast on it, "Anti-Magic area!" and then `sub087C mc013A, True`
(MAINMOD2.BAS:516-519). Both are reproduced.

**The loop stops one bit short of the flags, and that is what keeps the sheet consistent.**
`medDrawSquare` runs `For l0104 = 0 To 22` and blits from source x = `l0104 * 15`, so cell number
is flag bit number. But bit 23 is a real flag - the one that doubles your chance of being found
while you wait for rescue - and cell 23 is the first of the four magenta "you are here" arrows,
blitted separately as `Facing + 22`. A loop written to cover every flag would paint a north arrow
on every rescue square.

**Half the blitter is unreachable.** `medDrawSquare` takes a third parameter naming a bit, and
when that bit is *not* set it blits that cell with raster op 70 - whose low word is SRCINVERT's
`0x00660046` - against 134 for SRCPAINT on the set-bit path. All nine call sites pass -1, which no
bit index between 0 and 22 can equal, so the XOR half never runs. The port implements SRCPAINT and
the cursor's SRCCOPY and nothing else; that absence is this, and `selfcheck` counts the call sites
so it stays true.

The twenty-three are drawn here for the same reason they are drawn there: the routine is written
over the whole flag word, and narrowing it to the bits that can occur would be this port deciding
something the original does not.

**"The City" is listed in Window Control but has no size record.** The help's screenshot of the
Control Panel was taken in the city and its list reads `<ALL>`, Messages, `"Crashland"`,
Objects & Spells, Party Information, The City. That list is built from the visible MDI children's
captions, and no form sets a `gv19C2` index for the city screen - so `sub074E` looks it up, fails to
find one and returns. It can be brought to the front with Show; Reset and Adjust to Display cannot
touch it. Reproduced with a negative index that nothing in the table matches.

**The city used to draw a character window of this port's own invention, and two of its pages were
wrong.** It had five tabs - Stats, Char, Guild, Resist., Items - where the original has the same
seven-tab FRMCHAR1 in the city as in the dungeon. Its resistances page listed ten of the eleven
under names that were not `gv0620$`'s ("Electric" for Electrical, "Draining" for Drain, and no
Acid at all), and its items page numbered the compacted inventory rather than the forty slots the
pack actually has, so the numbers did not match the ones Swap Items takes or the ones the game's
own messages name. Both screens now use the one window.

## Companions and pens are keyed by the table index (2026-10-18)

The original names a creature in a companion and in the Confinement by its index in the monster
table, never by its Id. A companion record carries `Ref`, the index, and `M57D4`, the Id, side by
side (fn1163, MAINMOD2.BAS:388-389; cmdBuy_Click, FRMCITYC.BAS:128-129), and everything reads
`Monsters(Companions(i).Ref)`: its name on the Companions page (MAINMOD2.BAS:4735), its size for
carrying (2050), its turning chances (MONSMOD.BAS:539-542), its Library record (FRMCITYC.BAS:194,
FRMCHAR3.BAS:747-757). The pens are `gv162E(Ref)`, one record per table index holding `Align(0 To 3)`:
Buy takes from the row's index (FRMCITYC.BAS:141), Sell adds to the companion's Ref (288-291),
sub054E fills them by index (MAINMOD.BAS:542-557). The port kept only the Id, in the companion and
in the pens' keys ("312:0"), and MDATA5's Ids are not unique: **307 is the Morgul (index 278,
Neutral) and the Shadow Ninja (index 389, Evil)**. Their pens were one pen, a Shadow Ninja companion
read as a Morgul, and the Unaligned repair of 0c2b901 could only refuse to guess (OwnAlignment).

Now a companion carries `Ref` (`Companion.Ref`, `CompanionRef`), the charm, the join and Buy fill
it, every window and the combat engine look the creature up by it, and the pens are keyed
"tableIndex:alignment" with `"KeyedBy": "Ref"` in confinement.json. With the Ref known, an
Unaligned companion or pen is simply given its creature's own alignment and `OwnAlignment` is gone.
sub054E's ten are now exactly its table indices, `gv1DD2(kind, 0)` and the index after it; for the
shipped data they are the same ten creatures as before.

**Old saves, on load.** A companion saved without a Ref and a confinement.json without `KeyedBy`
are moved across once, and written back keyed by Ref:

- An Id that is one creature converts exactly (every Id but 307).
- Id 307 is settled by the one thing the record holds that tells the two apart, its alignment:
  fn1163 gives a charmed or joined companion its creature's own (MAINMOD2.BAS:392, from
  MONSMOD.BAS:1880), and the pens fill at the creature's own (MAINMOD.BAS:551, 1379). So Neutral is
  the Morgul and Evil the Shadow Ninja.
- Otherwise (Good, which only the Confinement's realign gives, or Unaligned) the original's own
  rule for a record it cannot place applies, rather than an invented one: the first table index
  with the Id, as the COMPANION SORTER does when `M57D4` no longer matches `Monsters(Ref).Id`
  (MAINMOD2.BAS:4720-4727, `Exit For`) - the Morgul. That holds for a companion (the port runs
  the same sorter on every load) and for a pen. The original's other mover, for a companion store
  file of another data version, copies a record to *every* index with its Id
  (MODULE1.BAS:836-842); for a pen that would make creatures up - a Good 307 pen of two would be
  four - so the port does not follow it there, and no count changes in the move. A pen whose Id no
  creature has is dropped, as that loop drops it.
- The pens are only ever read with the monster table to hand (`Confinement.Load` requires it),
  and a store still holding Id keys is never written back stamped `"KeyedBy": "Ref"`.

The user's save of 2026-10-18 has no companion and no 307 pen: its 21 pens all convert exactly,
and the Morlochs' "312:0" pen of two becomes the Morloch's Neutral pen (index 8). The Library was
still filed by Id at this point; it is keyed by table index now too - see the next entry.

## The Library is keyed by the table index (2026-10-18)

The original's Library file is one record per table index: `sub0F9A Ref, kind, record` reads
record `Ref + 6`, the items after the monsters (HALL.BAS:60-80), and `sub0FCB` writes it the same
way (675-693). Every caller passes an index. `sub06FE` files `p05D0.Ref`, the group's (or
companion's) table index (MONSMOD.BAS:3963, 4005), for a sighting, a recognition, a death and a
companion's ID or Info alike; `sub0730` files `pv065A`, the pack's or the shelf's item index
(MAINMOD2.BAS:4284, 4327); frmLibrary's `sub327E` lists index by index, one row per table entry
with a grade (FRMLIBRA.BAS:64-76), and a click looks the page up by name, the first index with it
(HALL.BAS:107-117) - every monster name in MDATA5 is unique, so that is the row's own index. Each
record's `Id` field holds the creature's Id (4004), and an item record's `M5C14`, the creature a
find came from, holds `Monsters(p065E).Id`, an Id, not an index (MAINMOD2.BAS:4306), which the
page turns back into a creature with `fn0E0D`, the first index with that Id (HALL.BAS:383,
MONSMOD.BAS:2269-2277), or index 0 when none has it.

The port keyed creature records by monster Id, and listed one row per Id. For 307 that was one
record for two creatures, the Morgul (index 278) and the Shadow Ninja (389): sightings of both
went into it, and only the Morgul was listed. Now:

- creature records are `"mr:index"` in library.json (`Librarian.Store.ForMonster`,
  `EditMonster`, `KnowsMonster`); `FileSighting` and `FileCompanion` file the table index, so the
  Morgul and the Shadow Ninja are filed, listed and paged apart;
- the Library page lists the whole table, a row per index with a graded record - the old list
  also dropped index 0 (GOBLIE, Id 0), which the original's loop includes;
- an item page's "the last one from a X" names `fn0E0D`'s creature (`Librarian.FinderName`): a
  find credited to 307 reads "Morgul", as in the original, and the chest still writes the Id;
- item records stay keyed by item Id, `"i:id"`. The original keys them by item index, but
  MDATA3's item Ids are unique, so an item Id is exactly one index and one record, and the port
  names items by Id everywhere else (pack, shelf, bank). Checked by the selfcheck, so data where
  two items share an Id would fail it.

**Old files, on load.** `"m:id"` keys are the records written before, by Id. When the Library is
loaded with the monster table (every place that reads or files a creature), each moves to its
index: an Id that is one creature exactly; **307 to the first index with it, the Morgul** -
`fn0E0D`'s rule, the original's own from an Id to a creature, used for this very file's monster
field, and the COMPANION SORTER's (MAINMOD2.BAS:4722-4727). A creature record holds nothing else
that tells the two apart (no alignment, unlike a companion), and copying it to both would make up
sightings, deaths and a first recorder for the Shadow Ninja. An Id no creature has is left under
its old key, untouched. A store loaded without the table (only the item filings do) refuses to
read or file creatures, and saves the `"m:"` keys exactly as they were - never relabelled as
indices.

**An older build.** The prefix itself is the format marker, per key: a build from before reads
`"m:"` keys only, so it neither shows nor files over `"mr:"` records, and its save keeps them as
dictionary entries it does not know. (A file-level marker would not survive: such a build does not
know the property, so its save drops it.) Whatever it files is a fresh `"m:"` record, and the next
load here moves it across; if the index already has a record the two are put together as the
filings would have gone on one record - the grade the higher, the first recorder the existing
one's if it had a grade, sightings and deaths added, the newer place and the later date.

The user's library.json of 2026-10-18 (a copy, inspected): 155 creature records and 118 item
records; no 307. All 155 move exactly - 151 to an index other than their Id (Giant Spider, Id 102,
is index 195), 4 where the two agree - with every field unchanged, and the Library lists the same
155 creatures before and after. Selfcheck "the Library is keyed by table index".

## Drag and drop

The help asks for it in three places - "You can also swap items and move items to the Store, Bank
and the Buffer Window by using Drag and Drop" (Items), "Your character has the ability to move
(Drag and Drop) spells and items into the slots" (Buffers), "This window also supports Drag and
Drop to the Buffer Window" (Spells) - and the original decides what was dropped from the dragged
control's Tag: `"ITEM"`, `"SPELL"` or `"BANK"` and a slot number.

**A buffer stores a pack slot, or a spell index plus forty.** `lblBuffer_DragDrop`
(FRMCHAR1.BAS:215-229) writes `Source.Index` for an item and `40 + spellIndex` for a spell, and
forty is the pack size. There is no refusal on that path at all: whatever you drop goes in.

**Swapping two items fixes up the buffers.** `lblItem_DragDrop`'s second branch (FRMCHAR3.BAS:559)
exchanges the two pack entries and then walks the ten buffers, pointing any that named one slot at
the other. Both slots must be non-zero - the first is the character's Hands and does not move.

**Taking an item back out of the bank can re-identify it, and clears any buffer that named the
slot.** The first branch of the same routine (FRMCHAR3.BAS:527-557) sets the retrieved item's ID
level to what the character can now make of it when that is better, says "You have re-identified
the retrieved item as: X", and sets every buffer pointing at that slot to -1 rather than remapping
it, because the item there is a different one.

Note which way the comparison runs. `M4EEE` counts **down** as you learn more - zero is knowing
everything - so the original's test is `fn12F0(...) < M4EEE`. This port's `IdLevel` counts the
other way, `Barely` to `All`, so the same test reads as greater-than here. Getting that backwards
would quietly un-identify everything that passed through the vault.

The bank's own refusal is the original's too: "Cannot put equipped items in the bank!"

**Anything dropped on Equip, Use, Drop or Info presses it (2026-10-18).** `cmdCommand_DragDrop`
(FRMCHAR3.BAS:252-254) is the one line `cmdCommand_Click Index`: it never reads `Source`, so a pack
line, a spell name, a companion, a vault line or a party name let go over one of Objects & Spells'
four commands is a click on it. The item it acts on is the one selected (`m001A`), and a dragged
pack line is that one: `lblItem_MouseMove` runs `lblItem_Click Index` before every `Drag`
(FRMCHAR3.BAS:589-597), so dragging an item to Drop drops that item. Nothing is said beyond what
the press itself says. It is the **only** command button with a DragDrop handler in any form -
every `_DragDrop` in decompiled-named is a label, text box, list, panel or picture, all of which
the port already took - and it is the same form in town and below, so both windows have it
(`PackCommands`, DragPayload.cs). The port's buttons had taken no drop at all. A disabled button
takes nothing, as a disabled VB control gets no events. Selfcheck pins the audit and the wiring;
Mordor.UiCheck drops each kind on the buttons laid over the list and checks that an item let go
on a line, clear of them, still changes places with it.

## A slot is not a place in a list

Everything that takes one of your things rolls a **slot** and looks in it. The chest's Slime, a
monster's destroy and its steal, and quicksand all do the same thing: roll a number, read `M5857`
at that number, and act only `If M5857(slot).M4ED4 > 0` (MONSMOD.BAS:3295-3296 and 3351-3352). A
gap in the pack is a miss.

This port had been reading the compacted inventory at that number instead. On a pack with no gaps
the two agree exactly, which is why it went unnoticed for so long; the moment anything is dropped,
sold, banked or swapped the pack has holes, and then they disagree about which item goes and about
whether anything goes at all. With three things in slots 5, 20 and 37, the old reading destroyed
one of them on a roll of 1, 2 or 3 and never touched them on a roll of 5, 20 or 37
(`Mordor.DataDump ... slotroll`).

The same mistake had already turned up in three other places and been fixed there: an item buffer's
description, the city's items page, and the bank's pack list. It is worth grepping for
`Inventory[` before adding anything new.

**The theft roll is wrong at both ends of the pack.** `MONSMOD.BAS:3351` is
`Int(Rnd * (mc04D0 - 1)) + 2` with `mc04D0 = 40`, so the draw is 2 to 40 against a forty-slot pack
numbered 0 to 39. The one line misses at the top and the bottom:

- **40 is one past the last slot.** One roll in thirty-nine reads off the end, and the original's
  own error handler swallows whatever that does. Reproduced as a miss, which is the visible effect
  either way.
- **1 is never drawn.** This entry used to record only the overrun, and this is the half that
  shows. Slot 0 holds the Hands and is no loss, but slot 1 is an ordinary pack slot: put your best
  item in the second row of the pack and no thief can ever take it.

The two together mean 39 of the 40 slots are in play but not the 39 you would expect, and one roll
in thirty-nine is spent on nothing at all. Both are asserted, and correcting the line to a plain
`Int(Rnd * 40)` fails five of the checks.

**The store's Combine box is an accumulator, not a picker.** `txtCombine_KeyPress`
(FRMCTYS.BAS:784-805) takes each item you name - typed and entered, or dropped on the box - appends
its slot number to `lblCombine` comma-separated, and clears the box ready for the next. The first
one named is the one everything else is poured into, and Combine lights up as soon as there is one.

This port used to build that list for you, out of "the item selected and every other copy of it".
It reached the same place for the ordinary case and was easier to use, but a list built that way
can only hold items that match - so three of the four answers `sub2621` can give were unreachable:
"is not compatible class", "is not of the same type", "is not compatible Alignment". Naming a
potion and then a cloak now says `STORE: Item #2 is not compatible class.` as it should.

One quirk kept: the original enables Combine after a single item is named, and pressing it then
does nothing at all, because the pour loop runs from the second item onwards. `Combining.Refusal`
returns an empty string for that case and the caller says nothing.

**The companion breath is built - 2026-09-22.**

The first of the three specials now works end to end, using the two rules built beside it. The
shape is MONSMOD.BAS:875-959 (the line numbers in this entry as first written, 69-150, were
counted from the top of the routine rather than the file):

1. Only a companion in slots 0 to 3 is asked - `mc01A0` is 4, the same four-of-five bound the acid
   draw reads past.
2. It must carry the fire bit or the cold bit (3 and 4 of `SpecialAttackFlags`).
3. It fires on a roll against 50, or **80** when the lead foe's guild level is more than a quarter
   above the companion's and **20** when it is more than a quarter below - so a companion is at its
   most dangerous against something far stronger than itself.
4. `fn0D64` then decides whether it lands on your own party instead of the monsters.
5. On a mishap the victim takes it and every one of *their* companions takes it too; each is rolled
   separately, so the splash is not the same number twice. A companion killed by it goes.
6. On anything else it sweeps every group. A kill brings the next member up to take it too, a
   survivor ends that group, and the loop is `Loop While ... l020C <= mc0230` with `mc0230` = 4,
   so it gives up after the **fifth** kill in a group, not the fourth as first written here.

**One breath a round - for cold breath only. Corrected 2026-09-25.** This entry said a character
holding three fire-breathers gets one breath between them. The guard is MONSMOD.BAS:875:

```
If testBit(mc0226, ...) = True Or testBit(mc0228, ...) = True And l0208 = False Then
```

and VB's `And` binds tighter than its `Or`, so that is `fire Or (cold And Not l0208)`. A fire
breather is never stopped by an earlier breath; only a cold one is. And `l0208` is not "a breath
happened": it is set at 957 when `l020C` is above nought, and `l020C` is the sweep's kill count,
zeroed at the start of *each* group - so what it holds is the last group's kills. A breath that
killed in the first group and only scorched the second leaves the guard open; one that went astray
never touches `l020C` and the test reads whatever an earlier companion's sweep left there. All of
it reproduced.

The mishap line is the original's, `"X: Companion #1 accidently breathed on "Y" and his
companions!"` - the possessive is `fn12DC`, "his" or "her" by the victim's sex; this port first had
"and Y's companions", which was its own wording. If the victim dies of it the routine leaves at
once (MONSMOD.BAS:900-903) *before* the line, so a fatal accident is announced only by the death,
and none of that character's later companions act that round. The victim, if on screen, gets
`"You were accidently Breathed on for N!"`. The sweep says nothing at all.

Measured over 3,000 rounds with one breather against two much weaker monsters: the breath fired
2,753 times, 2,735 of them at the monsters and 18 onto the party.

The electrocution and the Stone attack are built now as well; see "A companion's blow can land on
a friend" below.

**The acid spit is built - 2026-09-22.** The second of the three specials, sharing the mishap roll
and `monGetSpecialHits` with the breath but differing in three ways.

It has **no once-a-round guard**. The breath's block is gated on `l0208`; the spit's block at
MONSMOD.BAS:968 stands on its own, so every companion that can spit does, every round.

**Its mishap roll is not its own - corrected 2026-09-25.** The port rolled the spit's mishap inside
the spit. The original rolls it at MONSMOD.BAS:961-967, for every companion, *before* asking at 968
whether it can spit - and the victim it draws is the one the companion's ordinary blow then aims
at. The spit only reads it. See the entry on the blow below.

Where the breath splashes everything the victim owns, the spit **picks one thing**. `fn0DAB`
(MONSMOD.BAS:2100-2119) counts the living companions in slots 0 to 3 and lands on one of them
`count / (count + 1)` of the time, on the character otherwise - a coin flip with one companion,
two in three with two, three in four with three. The two outcomes have their own wordings, the
second naming both the companion and its owner:

```
"X: Companion #1 accidently spit on "Y"!"
"X: Companion #1 accidently spit on Companion #2 belonging to "Y"!"
```

And on no mishap it does **not sweep**. The front of the owner's group - `Foes(CombatOrders(owner).M547A)`,
MONSMOD.BAS:1007 - takes it and the attack is over, where the breath goes through every group. (The
port first aimed it at the first monster standing; it now aims where the owner aims.)

The line on a character carries `l01F8`, the breath's " and his companions", which is never cleared
between companions - so a spit that follows a stray breath in the same round reports companions it
never touched. Reproduced; it applies to the hit, stone and electrocution lines too.

**An uncertainty, stated rather than resolved.** The one call is
`fn0DAB(pv01E4, Companions(l0204).Ref, gc1DC2)` at MONSMOD.BAS:966, where `pv01E4` is the
*attacking* character - and the slot it returns indexes the **victim's** list. The victim is never
the attacker, since the mishap roll spares the owner, so the two lists are always different. Read
that way it is a fault: a slot counted from one list and applied to another can name an empty one.
But the decompiler recovered this function with a single parameter while noting it is called with
three, so the body may be using an argument that was lost, and on that reading it is the victim's
list and correct. There is only the one call site, so nothing settles it. The port follows the text
that exists and treats an empty slot as a miss, which is what the original's error handler makes
of it.

**A bit that was named wrongly.** `SpecialAttack` in Combat.cs had bit 5 down as DestroyItem. It is
the acid spit - the original names it `mc0232` and tests it at MONSMOD.BAS:968, and the port's own
monster-acid path already treats bit 5 as acid. Nothing used the constant, which is why the clash
survived: every test in this port is a bare `Bit(flags, n)` with the source line beside it. Bits 3,
4 and 5 are now marked as evidenced and the other eleven as inference.

**There are two breath formulas and they are not the same arithmetic.**

A monster breathing on you and a charmed creature breathing on your party do not use the same
calculation, and neither is a copy of the other gone wrong.

`sub0E85` inlines its own (MONSMOD.BAS:2702-2704):

```
l046C = monsterLevel * Log(monsterLevel + 1)
l0468 = Int((Log(l046C) + l046C / 15) / Log(1.08#) - currentGuildLevel / 20 + 1)
l0460 = ((Int((Rnd * l0468 + 1) / 2) + l0468 / 4) * .7#) / 2
```

`monGetSpecialHits` (MONSMOD.BAS:2391-2460) is what a companion's breath, acid spit and
electrocution call:

```
l040C = attackerLevel + 1
l040E = Int((Log(l040C) + l040C / 15) / Log(1.08#) - targetLevel / 10 + 1)
l0410 = (Int((Rnd * l040E + 1) / 2) + l040E / 4) * .7#          ' breath; / 2 for the other two
```

Three differences: the seed is `level * Log(level + 1)` in one and `level + 1` in the other, the
target's level is divided by 20 against 10, and the first halves its result. At attacker level 120
against a target of 40 the ranges are 830 and 415 - the monster's own is twice the companion's.
The port has both and `selfcheck` asserts that they disagree, because unifying them would look
like tidying up.

The rest of `monGetSpecialHits` is one shape with three fillings. All three share that range;
breath adds a quarter of it to the roll while acid spit and electrocution add a half, so a
creature's spit hurts more than its breath. Resistance then takes **half** its percentage off, as
everywhere else a special attack is resisted, and which resistance applies is read off the
**attacker**: a cold-breathing creature is resisted by Cold however good the victim's Fire
resistance happens to be.

The target's level is its top guild level if it is a character and its guild level if it is a
monster - the original says which by the sign of the argument, a negative number meaning a
character at index `Abs(n + 1)`.

`SpecialHits` carries all of this, and the companion's breath, spit, stone and electrocution
call it; see the entries above and below.

**Companions had no special attacks at all - now all are built (2026-09-25; the spell the same day, see "A companion casts").**

`monCompanionAttack` is 554 lines and most of them are the three specials a charmed creature can
use: breath, spit and a spell, plus Stone and Electrocute inside the melee. This port's companions
only ever made a plain attack when this was written.

What makes it worth its own entry is what the specials do when they go wrong. Before a breath,
spit or spell fires, `fn0D64` (MONSMOD.BAS:2020-2052) rolls to see whether it lands on **your own
party** instead of the monsters:

```
chance = (999 / companion's guild level) * (fn125B() / 20) * factor
                breath  factor 2    clamped 2 .. 40
                spell   factor 1.5  clamped 1 .. 40
                spit    factor 1    clamped 0.5 .. 20
```

Two things follow from the shape. A **weak** companion is far more dangerous than a strong one -
the guild level is in the denominator, so a level 1 creature is pinned to its ceiling whatever
else is true. And a **bigger party** misfires more often, because `fn125B` counts every character
and every living companion (its loop runs `0 To mc00E0 - 1`, so a fifth companion is not counted -
the same four-of-five bound the acid draw reads past).

The victim is then drawn from the party, and a draw that lands on the companion's **own owner** is
turned into no mishap at all rather than into somebody else. So a companion can breathe on anybody
in the party except the person holding its leash, and roughly a third of the mishaps in a party of
three are handed back to the monsters that way.

When it does land, the victim takes `monGetSpecialHits` damage and can die outright
(`mdrcharSetDead`), every one of *their* companions takes damage and can die, and the line that
follows is `"X: Companion #1 accidently breathed on "Y" and his companions!"` with a matching
`"You were accidently Breathed on for N!"` caption for whoever is watching. There are six such
verbs - breathed on, spit on, cast, hit, stoned, electrocuted.

**What is built.** `CompanionMishap` carries `fn0D64` and `fn125B` exactly, and `selfcheck`
asserts the three clamps, the arithmetic between them, both directions of the shape, the
four-of-five count, and that the owner is spared while everybody else is not.

**What was not, then.** Everything that would call it: the special attacks themselves, `monGetSpecialHits`
for a companion, the splash onto the victim's own companions, the deaths, and the eleven lines of
text. All of that is built now, the spell included; see "A companion's blow can land on a friend".

**A companion's ordinary blow says nothing - corrected 2026-09-22.**

I wrote here yesterday that "the melee log is this port's own voice", on the strength of finding
`"{who} lunges at the {monster} and misses."` in the combat code and nine short captions in
`monAttack`. That was wrong, and wrong in a way worth recording: I found a string, assumed which
routine it belonged to, and did not look.

**The character's own melee was already right.** The port has all nine of `monAttack`'s captions -
"You missed!", the four "You hit for N damage!" forms and the four kill forms - together with
"Using no weapon!" and "Weapon has no effect!", and it already reproduces the rule that only the
watched character's swings are shown: every caption write in MONSMOD.BAS:327-497 is guarded by
`If pv00F4 = gv1A86`. Nothing there needed changing.

**The companion path is what was invented.** `monCompanionAttack` writes exactly two captions
across its whole length, both for special attacks:

```
lblCharacterHit.Caption = fn128B(Me, slot) + " Stoned one!"
lblCharacterHit.Caption = fn128B(Me, slot) + " Electrocuted one!"
```

An ordinary companion hit, miss or kill produces **no text at all** - the monster simply has fewer
hits the next time the list is drawn. The port had it announcing every blow. Those lines are gone;
the damage, the credit for the kill and the critical and backstab rolls are untouched.

The naming rule the invented lines used is real and stays - `fn128B` for the companion, and the
leader's own named on its own while anybody else's is prefixed with their name in quotes and a
colon (MONSMOD.BAS:649-652). It is still what the special attacks use.

**A companion's blow can land on a friend - built 2026-09-25.** This answers the "not yet done"
that stood here. The port has the whole family now, except the one for a spell, and on the way it
turned up five places where the port's companion turn was not the original's.

*The friendly-fire draw is for every companion.* MONSMOD.BAS:961-967 reset the victim to none,
roll `fn0D64` at the spit's odds and, on a mishap, draw the slot with `fn0DAB` - all before the
spit bit is tested at 968. The ordinary blow (1085 onward) then aims at that victim. So a companion
with no special attacks at all still strikes a party member, or one of their companions, now and
then; a level 1 creature does it on up to one round in five. The only thing that replaces the
victim is the companion's spell (1045), built since ("A companion casts").

What the blow reads from each kind of target:

```
                  a monster              a character            a character's companion
defence           Foes(g).Def            Chars(v).Def           Companions(s).Def
level             its guild level        topGuildLevel          its monster's guild level
invisible         property bit 1         the Invisible state    property bit 1
```

`Chars(v).Def` is the stored figure, which does not include Protection - Protection is added only
inside the monster's own swing - so a friend's blow ignores it. An invisible target halves the blow
unless the companion has property bit 0 (`m0240`, a constant 0).

The lines, each the original's:

```
"You were accidently hit for N!"                       caption, if the victim is on screen
"X: Companion #1 accidently hit "Y"!"                  (+ the stale l01F8 suffix)
"X: Companion #1 accidently hit Companion #2 belonging to "Y"!"
"X: Companion #1 accidently stoned "Y"!"               and Y is dead of stone (cause 2)
"X: Companion #1 accidently stoned Companion #2 belonging to "Y"!"
"You were accidently electrocuted for N!"              caption - see below
"X: Companion #1 accidently electrocuted "Y"!"
"X: Companion #1 accidently electrocuted Companion #2 belonging to "Y"!"
"Companion #1 Stoned one!" / "Companion #1 Electrocuted one!"   caption, owner on screen
```

*Faults in the original, reproduced:*

1. **The stone reads the wrong creature's constitution against a monster.** The three branches at
   MONSMOD.BAS:1225-1231 are `Monsters(<the companion>).Stats(3)`, `Chars(victim).Stats(3) +
   StatMods(3)` and `Monsters(<victim's companion>).Stats(3)` - the first names the attacker where
   the others name the target. So a stoner turns monsters to stone at a rate set by its own CON,
   whatever it hits. `selfcheck` measures it: a CON 10 stoner stones a CON 3 foe and a CON 40 foe
   both at 74.85%, against the hand-computed 74.87%; reading the foe's CON instead gives 96.43% and
   5.14%, and the check fails on both.
2. **The electrocution caption asks nobody.** Every other caption in the routine is inside
   `If l0214 = Me`; the one at MONSMOD.BAS:1300 is not. Whoever the companion electrocuted, the
   character on screen is told "You were accidently electrocuted for N!". The line after it, which
   names the real victim, is only written if they survive.
3. **The follow-ups ask about the owner's group, whoever was hit.** Stone and electrocution are
   gated on `l01F0 > 0 And Foes(M547A).Hits > 0` (1215). When the blow killed the front monster,
   `sub0D08` has already loaded the next member into that record, so the stone and the shock land
   on the *next* monster of the group; when the blow hit a friend, the condition is about a monster
   nobody touched.
4. **A death ends the owner's turn.** Every `mdrcharSetDead` in the routine is followed by
   `Exit Sub`, so a companion that kills a party member by accident is the last of its owner's
   companions to act that round.

*The port's own mistakes, fixed:*

1. The critical and backstab were tested on the companion's **property** bits 10 and 11; the
   original tests `SpecialAttackFlags` (mc023C = 10, mc023E = 11, both declared in the routine).
2. The spit's mishap was rolled inside the spit, so a non-spitter could never misfire at all.
3. The breath and spit lines went out on the log channel, which the dungeon window skips - so no
   companion accident was ever shown in the message box. They are messages now.
4. The companion aimed at the first monster standing; it now aims at `Foes(CombatOrders(owner).M547A)`,
   the owner's group, with the next member stepping up when the front one falls.
5. A `DamageMod` of 0 or less was treated as 1. No creature in MDATA5 has one, and the original has
   no such guard, so it is gone.

**A stone on the last of a group is followed by its electrocution - corrected 2026-10-18.** The
port had ended the attack there. The stone does not zero the record: it credits the hits, puts up
"Stoned one!", runs sub0CF3, takes one off GroupCount and calls sub0D08 (MONSMOD.BAS:1241-1249),
which with nobody left reloads nothing (1995, 2009-2017). The electrocution's gate, `Foes(M547A).Hits
> 0`, was tested before the stone (1215), so a companion with the bit goes on to electrocute the
stoned monster's record at the hits it had (1268-1296): monGetSpecialHits' draws, SHOCK, experience
for what it takes off, and at nought "Electrocuted one!", sub0CF3's quest test again, and GroupCount
down to -1. The port strikes a copy of the stoned member at its hits, outside the list. The -1
GroupCount has no field here: its later effects - monShowCursor re-aims only at exactly 0
(MONSMOD.BAS:4207), so does sub0BDE (MULTIMOD.BAS:162), and a turning companion takes only a slot at
exactly 0 (MONSMOD.BAS:557) - are not reproduced. (The companion spell block, "accidently cast" included,
was built later the same day - see below.)

**A deadly chest trap counts the death twice on the 'o' key - the original's, checked 2026-10-18.**
After the trap and the party list's UPDATE, monOpenChest sets the opener's slot dead when the
leader is below 1 (MONSMOD.BAS:3653-3658), and mdrcharSetDead counts a death (MAINMOD2.BAS:4578).
On the 'o' key fn1521's ending then finds the character below 1, counts another and runs sub0930
(4089-4091), whose own mdrcharSetDead does nothing for one already dead (3042): two deaths on the
record. The count-down open has no such ending (MULTIMOD.BAS:527), so one. The port counts the
same. When the opener was not in slot 0 and the prune left one character, mdrcharSetDead's slot is
gone and its bare `Resume` (MAINMOD2.BAS:4587) hangs the game; the port sets nobody dead there, and
the ending's count and sub0930's mdrcharSetDead (now on a live character) make the same two - or
the four-second tick's one on the count-down open. Necessary: a hang cannot be ported.

**Six chest-trap messages were this port's own - removed 2026-09-22.** Across all ten arms,
`monOpenChest` calls `sub085B` exactly twice:

```
sub085B "SLIME: " + Items(...).Name + " Destroyed!"        ' MONSMOD.BAS:3546
sub085B "Your companions were scared away!"                ' MONSMOD.BAS:3624
```

Everything else is silent. What tells you which trap fired is its **name**, written into
`lblTrap.Caption` from `gv08AA$` (MONSMOD.BAS:3560) - not a sentence describing it. The port
already carries that, as `_trapCaption`, and on top of it had invented six lines: the gas
catching you, the fire catching you, fate turning, the floor dropping away, everything going
dark, and withering. All six are gone and `selfcheck` now asserts the whole message set rather
than their absence, so a seventh cannot appear quietly.

**How they were found.** Not by reading the file - by asking, of every string literal in
`Mordor.Core` long enough to be a sentence, whether it appears anywhere in the decompiled source.
Forty-six did not. Most were doc-comment `see cref` values and quoted VB fragments, but six were
messages the game prints, and those six were all in one place. The same question is worth asking
again as the port grows; it is the only mechanical test of "nothing invented" there is.

Two strings outside the traps came up in the same sweep and are left for now, recorded here so
they are not lost: `"The trap kills you."` and `"You walked into solid rock!"` are death reasons
this port composes, where the original stores a numbered cause. Neither is printed as a message
line, which is why they are a smaller question than the six above.

## Dead code in the original that the port does not carry

**A dead per-guild tally during a log clean.** `sub316B` fills `l0060(guild)` as it copies
entries forward and never reads it. Nothing else in the sub mentions the array. The port does
not carry it.

`sub0980` ends with a second fog pass at MODULE3.BAS:1443, `If m01C6 = True Then`, that blacks
out all four quadrants. Line 1352 sets `m01C6 = False` immediately before it and nothing between
the two touches the variable - those are its only three mentions in the whole module. The block
can never run, so `MazeView` has nothing for it. Reproducing unreachable code would not be
fidelity, but it is recorded here so the omission is not mistaken for an oversight.

**A flag the monster turn sets twice and never reads.** `sub0E85` clears `l0476` at
MONSMOD.BAS:2828 and sets it at 2848 (the spell path) and 2907 (a melee blow aimed at the
character). Those are its only three mentions in the module - nothing ever tests it. The port
had carried it as a `charHit` local, which it also never read; both are gone.

## Open questions - things I will not guess at

**"Party Settings" is in the help but not in the game.**

The help's Parties page describes the Party Options window as having two halves, Combat Settings
and "Party Settings - these are the general settings for the party". The form, `frmPartyOptions`
(FRMPINFO.FRM, all 642 bytes of it), contains a character list, four option buttons and a combo
box, and nothing else. There is no second half. Either it was cut before release or the help is
describing the global Options form, which does have a "Game Settings" group. Nothing to port.


**`fn13D4`: solved.** Kept here because the two mistakes are worth remembering.

The function is at MODULE6.BAS:2675, not MAINMOD2. Two things had it wrong:

1. **The caller keeps its answer on the character.** FRMCTYG.BAS:377-392 stores it in `M59A1`,
   refreshes it only when it has fallen behind the level, and recomputes it after every level-up.
   So the test at level L is `L Mod M59A1` against the value fn13D4 produced at **L-1**, not
   against a freshly computed interval. Read that way, `fn13D4` returns the next level an award
   falls on, and the modulus fires when the level reaches it exactly.
2. **The step is an Integer.** `l0434 As Integer` means
   `((Int(cap) / 2) + (acc / 18)) / (AdEvery + bonus)` is rounded before it is added, not
   accumulated as a fraction. Without that, every guild drifts within a few levels.

A third slip was mine alone: I first transcribed `Int(cap) / 2` as C# integer division. That is
exact for an even cap and half a point out for an odd one, which is why Nomad (30) and Warrior
(28) came out right while Paladin (29), Thief (21) and Seeker (27) did not - a tidy reminder that
"most guilds work" is not the same as "the rule is right".

**332 of 332** published award levels now reproduce, across all five guilds whose wiki pages hold
real data. The other seven pages are the Nomad table copied as a placeholder, which the index page
says outright, so they are not evidence either way. The port no longer consults the published
tables at all - the data-driven rule runs at every level, and the tables are a regression check.

**`monOpenChest`: poison gas and disease spores read backwards.**

The two ailment traps afflict a character when a roll falls *under* half their resistance:

    Case m0540                                    ' Poison Gas
        For each party member
            If (Rnd * 100) < (Resistances(5) / 2) Then
                Ailments(0) = DungeonLevel        ' poisoned, at the strength of the level

Everywhere else in the game that array reduces harm - `monGetSpellHits` multiplies damage by
`(1 - Resistances(i) / 100)`, and `monGetSpecialHits` by `(1 - Resistances(i) / 2 / 100)`. Here a
higher resistance makes the trap *more* likely to catch you, and a character with no poison
resistance at all can never be poisoned by poison gas.

**Settled 2026-09-22.** This entry used to end by saying the slip was "either a genuine slip in
the original... or the constants index something other than resistance", and that settling it
would mean watching a well-resisted character walk into poison gas under OTVDM. It does not. The
answer is nine lines further down the same `Select Case`:

```
Case mc054A                                       ' Fire Trap, MONSMOD.BAS:3536
    l0502 = l0502 - (l0502 * (((Resistances(0) / 2) * .01#)))
```

The fire trap reads the *same array* with the *same halving* and uses it the right way round, to
take damage off. And all three name the correct kind - `m0416` is 0 for fire, `mc04C2` is 4 for
disease, `mc04BA` is 5 for poison. So the array is resistance, the constants are right, and what
is wrong is the direction of those two tests alone. No emulator run is needed.

The consequence is very visible and is reproduced: for a character with no resistance the two
traps do nothing at all, and for one who resists they fire at half the resistance. Measured -
a Human at 0% is poisoned 0 times in 4,000 openings; a Giant at 50% is poisoned 25.2% of the time;
and the fire trap beside them takes 44.9% off a character warded at 90%, which is that same half
applied the other way up. Turning either test round fails the checks.


**The pack's slot zero holds your hands.** The original's pack is a fixed array of forty slots, and
slot zero is the character's own hands. MDATA3's item 0 is literally called "Hands" - no attack, no
defence, no price, and two hands wide - and the help's screenshot of the Objects & Spells window
(`game_files/bm14.png`) shows it at the top of the list as "1. Hands", not in bold, because it is
only worn when you fight bare-handed.

That explains the three rules around it: giving an item scans `For l04A4 = 1 To mc0190 - 1`
(MAINMOD2.BAS:2941) so nothing is ever put there, Drop refuses `If m001A > 0` (FRMCHAR3.BAS:212)
because you cannot drop your own hands, and `sub0F74` lets slot zero hold item id 0 where no other
slot may. So a character carries thirty-nine things besides their hands, and the slot numbers in
the original's own messages count the hands as number one.

The port's pack was a list, so index zero was a real item, the slot-zero Drop refusal could not be
reproduced, and removing an item shifted every index after it and silently repointed the spell
buffers. That is all gone: the pack is forty fixed slots with the Hands pinned in slot 0
(`Character.Pack`), Drop refuses slot 0 as the original does, and a slot is blanked in place, so
nothing is repointed.

## The morgue has two separate paths

Worth recording because I got this wrong once. MAINMOD2.BAS handles **your own** death - the body
is in the morgue and you are looking at it - while FRMMORG.BAS handles a *living* character walking
in and raising somebody else by name. They are different code with different rules.

The self-resurrection path, which is the one the port implements:

    l04CC = Int(2912.32# * (MaxHits / 1.5#))
    "A small fee of N is required for your Resurrection."
    If fn1373(char) <= 5 Then                       ' highest guild level 5 or below
        If Gold + BankGold < l04CC Then
            "However, since you are just starting out, the guild offers to pay for your
             resurrection.  Is this acceptable?"    ' and nothing is charged
    Else
        "Do you wish to pay?"
    "Chance of success is N%."

So **a character at guild level 5 or below who cannot afford the fee has it paid by their guild**.
Not a discount and not a loan - the charge is simply skipped. Above level 5, or if you *can*
afford it, you pay; and if you say yes without the gold you get "Not Enough Gold!" and nothing
happens at all. The raise can still fail (`35 * (CON / 4.5) + 40`, clamped 10..98), failure can
bring "There are complications...", success ages you 54 days and often costs a point of
Constitution.

The other path - raising *another* character by name from the morgue counter, as a living
character sees it (FRMMORG.BAS, game_files/bm10.png) - is built since (`Morgue`, PendingRaise.cs).

## A fault in the original that the port reproduces: two Ninja titles are unreachable

`fn12C4` picks a guild title with a `Select Case` on the level. The Ninja branch has three
consecutive identical tests:

    Case Is < 120
        fn12C4 = "Assassin"
    Case Is < 120
        fn12C4 = "Ninja"
    Case Is < 120
        fn12C4 = "Master Ninja"

VB takes the first case that matches, so the second and third are unreachable. A Ninja goes
Instructor, Assassin, then straight to Shogun at 120 and **never holds the titles "Ninja" or
"Master Ninja"** - the two named after their own guild.

**Corrected 2026-09-22.** This section used to end "The port matches on the first band that fits,
which reproduces this exactly. It is listed here so nobody later 'fixes' it." That was wrong twice
over: the port does *not* reproduce it by default, and the thing it warned against had already
been done: a house rule, `HouseRules.NinjaTitlesReachable`, gave the two titles invented
thresholds and defaulted to on. It was turned off on 2026-10-18 and **removed entirely on
2026-10-25**, so the port now has only the original's ladder and reproduces this exactly: Assassin
below 120, Shogun above it, and the two guild-named titles unreachable at every level from 1 to
400. It is a fault reproduced as written, not an oversight in the port. `selfcheck` asserts the
ladder, so it cannot drift without a check noticing.

## What the three faults were probably meant to be

The port reproduces each of these as written. This section records what the intent looks like, for
whenever a quality-of-life layer wants the option of the corrected behaviour - and to make clear
which readings are evidenced and which are inference.

### 1. Trap resistance polarity - **evidenced, not inference**

The chest trap in `monOpenChest`:

    If (Rnd * 100) < (Chars(i).Resistances(5) / 2) Then    ' -> poisoned

The nearest evidence is nine lines down the **same `Select Case`**: the fire trap reads the same
array with the same halving and uses it the right way round, to take damage off rather than to
decide whether you are hit.

    l0502 = l0502 - (l0502 * (((Chars(i).Resistances(0) / 2) * .01#)))

All three name the correct kind - `m0416` is 0 for fire, `mc04C2` is 4 for disease, `mc04BA` is 5
for poison - so the array is resistance, the constants are right, and only the direction of the
two ailment tests is wrong.

The monster poison attack elsewhere in the same file agrees, using **the same ailment index and
the same resistance index** as the trap:

    l0468 = Int(Rnd * 100) + 1
    If l0468 > Chars(c).Resistances(5) Then                ' -> poisoned
        If Chars(c).Ailments(0) = 0 Then ...

Same effect, opposite comparison. The combat version is the sensible one - a high resistance
protects you - so the trap line is an inverted test. The intended line was almost certainly
`> (Resistances(5) / 2)`: the same roll-beats-resistance check, at the half strength the rest of
the trap and breath code uses. The combat version also guards against re-poisoning someone already
poisoned, which the trap version omits, so the trap code looks like the hastier of the two.

### 2. Gold depth term - inference, but a strong one

    (Log((Floor + 1) / Log(2)))        ' as written
    (Log(Floor + 1) / Log(2))          ' log base 2, the obvious intent

`Log(x) / Log(2)` is the standard idiom for a base-2 logarithm in a language with no `Log2`, and
VB has none. One parenthesis is in the wrong place. The effect is systematic rather than dramatic:

| floor | written | intended | as written |
|---|---|---|---|
| 1 | 1.060 | 1.000 | 106% |
| 5 | 2.158 | 2.585 | 83% |
| 15 | 3.139 | 4.000 | 78% |

So deep floors pay about a fifth less gold than intended, and floor 1 slightly more.

### 3. Ninja titles - the intent is clear, the numbers are not

All eleven other guilds have strictly ascending title thresholds; only the Ninja does not, and it
has exactly two out of order. Each of the three titles at 120 is distinct and deliberately written,
and two of them are the ones named after the guild itself, which no designer leaves unreachable. So
three separate bands were meant, somewhere between the 90 below and the 150 above, and the numbers
were never filled in - the shape of a copy-pasted line edited only on its text.

What those three numbers should have been is not recoverable, and anything picked is invention.
This paragraph used to end "so the port leaves the titles unreachable exactly as the original
does", which was not true while `HouseRules.NinjaTitlesReachable` defaulted to on. That switch,
and the thresholds it chose (120-129 and 130-139), are gone since 2026-10-25: the port leaves the
titles unreachable as the original does, and has no other ladder to offer.

## Window layout - M5958 and the Control Panel

Every window the game shows has an entry in a table of defaults (`sub03A2`, MODULE1.BAS:606-648)
and a copy of its own size and position on the character (`M5958`). The defaults are in twips,
fifteen to the pixel at the 96dpi the game was drawn for; the one exception is the Automap, whose
entry is already in pixels, which is why character creation treats index 18 on its own and why
`FRMAUTOM.BAS:152-159` compares `Me.Width / TwipsPerPixelX` against it.

The conversion checks out against the help: the Dungeon's 7320x3450 twips is 488x230 pixels, and
the help's screenshot of that window is 487x229 - the client area inside a one-pixel border.

**Switching character does not rearrange the windows.** Every *reader* of `M5958` uses character
0, so the arrangement you see is always the leader's however many people are in the party and
whoever has the focus. Alt+1..4 changes who the windows are *about*, not how they are laid out.
Ported as written.

The reason is the readers, not the writers. This entry used to say `FRMAUTOM`, `FRMDUNM` and the
Control Panel "all write slot 0". Only the first does: `FRMDUNM` saves through `sub1614 -1`, to
every character in the party, as eleven other forms do. See
[the Reset entry](#reset-files-the-new-window-sizes-under-a-character-nothing-reads-them-back-from)
for the full census of who writes where - it is the same mistake, and it was in two places.

**Everything really does open in the top left corner.** `FRMDUNM.BAS:254-255`,
`FRMAUTOM.BAS:120-121` and the rest set `Me.Left` and `Me.Top` from `M5958`, and character creation
seeds only widths and heights. So a character who has never arranged anything gets every window
stacked at 0,0 and drags them apart by hand; `sub0524` only pulls back a window left off the edge
of a bigger screen. That is why the help has a Window Control section at all. The port does the
same, and saves where you leave them.

**The Messages window keeps twenty lines.** `sub085B` (MODULE6.BAS:3190-3203) adds a line and then
drops the oldest as soon as the count passes `mc04F2 = 20`, and `lstMessages_DblClick`
(FRMCHAR4.BAS:84-88) empties the list. The port had invented two hundred lines and no way to clear
them; both are fixed.

**The help lists five Character Options; the form has four.** Hide, Preload Character, No Perception
Msgs. and Auto Change Lev. are all on `FRMCONTR` and in the help's own screenshot
(game_files/bm0.png). "No Party Sound", which the help describes between them, has no control on
the form and no `M5914` slot that anything reads. Left out, like "Party Settings".

## House rules - there are none

The port has no house rules: no rule of the game departs from the original on purpose, and there is
no switch to choose between the original's behaviour and a corrected one. Where the original is faulty, the
fault is reproduced as written and recorded in this document.

There was one. `Mordor.Core/HouseRules.cs` held `NinjaTitlesReachable`, which gave the two
unreachable Ninja titles invented thresholds; it was turned off on 2026-10-18 and removed, with the
file, on 2026-10-25. See [the fault it covered](#a-fault-in-the-original-that-the-port-reproduces-two-ninja-titles-are-unreachable).

What is left under this heading is not rules of the game. Three entries are departures in how the
port presents things, with nothing to toggle between:

- where a window is drawn, and how small it may be drawn (two entries)
- which index the port keeps for the current character

and six entries that are not departures at all but sit here for context: what the original's own
key handler does in town, the `frmInteract` box, the Give command's three questions and the
Messages window's repeated lines (both departures until they were corrected), the disease cure
roll, and the correction the source forced on the published identification page. The port
reproduces them - all but the name box's per-tag refusals, which its entry spells out. They stay in
this section because they were found while hunting for departures, and moving them would lose the
trail.

Three presentation departures and six entries that are not departures: nine, and that is the whole
section.

### The window layout is measured against the frame, not the screen

`FRMCONTR.BAS:81-82` and `CHARCREA.BAS:307-308` scale every window by
`(Screen.Width / TwipsPerPixelX) / 800`, and the same over 600 for height, never below 1. There the
MDI parent is the whole display, so a child scaled against the screen still fits inside it.

The port's frame is an ordinary window, usually about 1200x780. Measuring against a 2560x1440
display would multiply every window by 3.2 and 2.4 and leave nothing that fits. So the port hands
the same arithmetic the frame's own size, which is the thing the original was really measuring.
`WindowLayout.ScaleFor` is untouched; only what is given to it changes (`DungeonWindow.ScaledFrameSize`).

This one has no switch, because it is not a rule of the game - it is where a window is drawn.

### Which Game Commands are dead in town, and which the port wrongly killed

`fn1521` (MAINMOD2.BAS:3283-3730) is the routine every form's KeyDown passes through, which is why
the help can say "Game commands will work in both the City and the Dungeon (any time)". It is not
quite that simple, and the port had it wrong in both directions.

Three of its cases begin with a test on where you are, and so do nothing at all in town - silently,
with no message:

| Key | Case | Guard |
|---|---|---|
| `c` Cast | 67 | `If Chars(Me).DungeonLevel = 0 Or DeathState <> 0 Then Exit Function` |
| `i` Item Info | 73 | `If Chars(Me).DungeonLevel > 0 Then ...` |
| `u` Use Item | 85 | `If Chars(Me).DungeonLevel > 0 Then ...` |

The port matches these by not binding them in the city at all, which comes to the same thing. It is
recorded here so that nobody later "fixes" the city by adding them. Note that the *buttons* are a
different matter: `cmdCast_Click` does answer, in words, "Cannot cast spells while in the city!"
(FRMCHAR3.BAS:156-159). The key says nothing; the button speaks.

Four have no such guard and work in town:

| Key | Case |
|---|---|
| `e` Equip | 69 |
| `g` Give Item/Gold | 71 |
| `l` Drop Body | 76 |
| `p` Pickup Character | 80 |

The port refused all of `g`, `p` and `l` in the city with **"Not in the city."** - a line of this
port's own, on commands the original allows. It is gone, and all three work in town now.

### One box makes most of the refusals: `frmInteract`

Every command that needs another character's name goes through the same box. `fn170D` loads
`frmInteract` with a tag - "JOIN", "PICKUP", "GIVE", "LOCATE", "FETCH", "SPELLRAISE" - and the box
validates the typed name against that tag (FRMINTER.BAS:154-250). That is why these lines are
worded as they are, talking about "Character" rather than about the command:

| Line | Tags |
|---|---|
| `Not Found!` | all of them |
| `ambiguous!` (lower case, as written) | all of them |
| `Yourself?  HA!` (two spaces) | PICKUP, LOCATE, GIVE, JOIN |
| `Character is not here!` | everything except LOCATE |
| `Character is Dead!` | JOIN |
| `Character is already in Party!` | JOIN |
| `Character is not Dead!` | PICKUP, SPELLRAISE |

`Character is not here!` would matter most, because its test settles a rule that was otherwise
only in the help:
`MorgueList(n).DungeonLevel <> Chars(Me).DungeonLevel Or MorgueList(n).M547A <> Chars(Me).M59BB` -
the level **and** the area - so "they must be in the same room, or in the city with you" (help,
Parties). The same test is Form_Resize's, which leaves such a character off the list (FRMINTER.BAS:377-380),
and that is where it is enforced, for every command that names somebody but LOCATE, FETCH and LOADCHAR. In town both
sides are the city, so everybody above ground is "here", which is why `p` works in the city at all.

An earlier note in this file said the original gives no reason for a candidate being out of reach
and simply does not offer them. That was right after all: the reason line is never reached (below).

The box takes a typed name now (`NamePicker`), so `Not Found!` and `ambiguous!` are shown as the
original shows them, which this entry once said could not be. The rest of the per-tag refusals
are not reached in the original either (settled 2026-10-19): they are cmdOK_Click's txtQuestion
branch (FRMINTER.BAS:154-250), which runs only while cmbCharacter is hidden, and Form_Resize shows
cmbCharacter for every tag that has them - with a list when anyone is on it (392-396), greyed with
its sentence when nobody is (453-455). So the original, like the port, offers only the filtered
list, and a typed name of yourself, of someone absent or of someone living gets `Not Found!` from
the combo's own match (66-101). The lines above are the original's words, kept in Interact, but no
command says them.

### 'g' Give asks the original's three questions - no longer a departure (corrected 2026-10-18)

`fn1521` case 71 asks for a character's name, then an amount of gold, then an item number - three
`fn170D` boxes in a row (MAINMOD2.BAS:3705-3730) - and hands both to `sub14B5`, gold first. The port
used to hand the selected pack item, or all the gold on hand when nothing was selected, to the next
member round. It now asks the three, as `GiveFlow` and `GiveCommand`:

- **"Give to Character" / "Character's Name?"** - the party but the giver (`gv1A84`,
  FRMINTER.BAS:331-338), typed or picked, with "Not Found!" and "ambiguous!" as the name box has
  them. Cancel ends the command (-1).
- **"Give to "Name"" / "How much Gold?"** (MaxLength 10) - empty, or a `Val` under 1, is 0; more
  than is on hand is `MsgBox "Not enough gold!", 48, "SORRY!"`, the box emptied and asked again;
  otherwise the text goes back **as typed** and is `CLng`'d (FRMINTER.BAS:121-132, 284-285). Text
  whose `Val` passes but which is not a number throughout ("12 gold") is a Type mismatch in `CLng`,
  which fn1521's trap swallows (`sub04FC` writes only to the error file): the command ends with
  nothing given and the item never asked. `Val` is VB's in full - blanks stripped anywhere, E and D
  exponents, `&H`/`&O` - so "1e3" is a thousand and refused with 500 on hand (the first cut of this
  read it as 1 and let a thousand through). **The original's own hole, kept:** the test is `Val` and
  the transfer `CLng`, and only `CLng` reads the Control Panel's thousands separator. "1,000" is 1 to
  the test and 1,000 to the transfer, so with 500 on hand it passes and hands over a thousand,
  leaving the giver at -500. A number past a Double is an Overflow in cmdOK_Click, which has no
  handler there; the port refuses it as more than is on hand instead of stopping.
- **"Give to "Name"" / "Which Item #?"** (MaxLength 3) - empty or "0" is no item; otherwise
  `fn131A` must name a slot, and slot 0 - item #1, the Hands - is refused with the empty ones as
  "No Item to give!"; a worn one is "Item is Equipped!" (FRMINTER.BAS:133-151). The answer's `Val`
  into an Integer, less one when above 1, is the slot (MAINMOD2.BAS:3720-3728).
- `sub14B5` then moves the gold (no second check) and says "Give of N gold to "Name" Completed!",
  then the item with its three refusals - guild issue and worn on Messages, a full pack in a box -
  and "Give of Item to "Name" Completed!" (MAINMOD2.BAS:2930-3006).

Selfcheck "'g' Give: name, gold, item #". The refusal when there is nobody to give to is
unchanged - "Nobody in party to give to!". Same for ALT-p, where the three lines *are* the original's:
"Cannot pool gold while in combat!", "Nobody else in party to Pool to!", and
`Pool of party's on-hand gold to "Name" completed!`. That last one is said whenever the party has
more than one member, whether or not any gold moved - the refusal is on the size of the party, not
on the result.

### The Messages window prints the same line twice running - no longer a departure

`sub085B` adds whatever it is given, every time, with no check (MAINMOD2.BAS:3195-3208), and so
does the port now: `MessageList.Add` keeps a repeated line and holds back only an empty one, which
the port's callers pass to mean "nothing to say". The rest of this entry is why the port once
dropped a repeat, kept for the trail.

That was not tidiness for its own sake. In the original the caption under the maze view and the
Messages window are written by different calls; in the port the caption feeds the Messages window,
so without the check a caption that is rewritten with the same text - which happens on any
redraw - would stack up as repeats the original never produced. The check bought that back.

What it cost: the ailment clock asks every four seconds and the original fills all twenty lines
with `Brannoc - "Help! My hits are 5/15!`, where the port showed it once until something else was
said. The port now shows every one, as the original does.

### One current character where the original keeps three

The original holds **three** indices, not two, and this entry used to say two.

| | what it is | moved by |
|---|---|---|
| `gv1A86` | the current character. 1,260 references - almost everything reads this one | both switches |
| `gv1A84` | who is being repainted and acted on: passed to `sub0F61`, read for position and stats | both switches |
| `gv1A82` | 96 references, nearly all of them the companion displays (`M58CB` in FRMCHAR3 and FRMCITYC), the give-source in FRMGROUP, the Control Panel's Reset and the batch window save | the full switch only |

Two routines change character and they do not change the same ones. `sub1536`
(MULTIMOD.BAS:651) sets all three and repaints the character pages, the companions and all forty
pack slots. `sub178B` (MULTIMOD.BAS:600) sets `gv1A84` and `gv1A86` and leaves `gv1A82` behind. A
single click on a party name calls the second; a double click calls the first
(FRMGROUP.BAS:171-179), and the help's Commands page lists the double click as the way to switch.

So it is `gv1A82` alone that goes stale, and two places in the source test for exactly that and
put it right: `If gv1A82 <> gv1A86 Then sub1536 gv1A86` (FRMGROUP.BAS:32, MAINMOD2.BAS:3413).
That lag is what the
[window-size entry](#reset-files-the-new-window-sizes-under-a-character-nothing-reads-them-back-from)
and the gold announcement's dead branch both turn on.

The help says plainly why the split exists: "You may notice that when you switch characters in the
dungeon, all of the windows don't automatically update. The reason for this is because it sometimes
takes a few seconds to repaint all of the windows (on slower machines), and when in combat, wasting
this amount of time in the middle of a fight for a repaint is unacceptable."

So it avoids a repaint that took seconds in 1995 and takes no measurable time here. The port keeps
one index and repaints the character window on every switch, which means a single click on a party
name shows you that character straight away instead of leaving the old one on screen until you
press Switch To.

The two commands are still kept apart where it can be seen - a single click switches, a double
click and the Switch To button also refresh the pack, the companions and the spell list - so the
shape of the original survives even though the staleness does not.

**Does any command act on a different character? - assessed 2026-10-21.** No. Every read of
`gv1A82` was gone through, and none decides whose stats, pack, gold or spell a command
uses; those all read `Me` (`gv1A86`), which both switches move, and `gv1A84` equals `Me` after
either. What `gv1A82` decides is:
- **Which window shows whom**: mdrcharStats paints `gv1A82` and skips the update when `Me` differs
  (MAINMOD2.BAS:5579-5582), the companions page (4716), the buffers after an item goes (4346, 5705),
  the carried-body line (4680), the combat cursor (MULTIMOD.BAS:547), the Look page's own row and
  "(You are Dead)" (MAZEMOD.BAS:279-312), and the window sizes saved and reset (MAINMOD2.BAS:4410-4451).
- **The wording of a message**, the plain form for the character on screen and the named form for
  anyone else: the cast refusals (MAINMOD2.BAS:448-512), "was too big to carry" (2061), the give
  lines (2966, 2999), "Your companion refuses to join" (MONSMOD.BAS:547), "has turned on you!" (577),
  "Left!" (654), the pit's sound (MAZEMOD.BAS:1761-1778) and the gold recovered amount (MONSMOD.BAS:2170).
- **The giver of a drag onto a party name** (FRMGROUP.BAS:192-209): the source is a pack line,
  the gold or a Bank line. The first two live on frmCharStat3 and frmCharStat1, whose Form_Activate
  runs `sub1536 Me` when they lag (FRMCHAR3.BAS:304-308, FRMCHAR1.BAS:33-37) - the mouse press that
  starts the drag activates the form first, so `gv1A82` is `Me` by the drop. The Bank exists only
  in town, where mdrmulSwitch itself ends in `sub1536` (MULTIMOD.BAS:638), so the three never part.

So the split changes what is on screen and how a line is worded between a single click and the
next full switch, and never which character a key or a command works on. The port keeps its one
index, as above; `_shownSlot` (M5E4F) carries the lag where the original's repaint tests read it.

### A window is never given less room than its contents need

The original's Dungeon window is 488x230 pixels and holds a small 3D view; the help's own
screenshot of it (game_files/dmainw1.png) is 487x229. The port draws that view at 430x360, so the
original's size would cut the window in half. Each MDI child therefore carries a minimum of what
its contents need, and Reset and Adjust to Display clamp up to it. Everything else about `M5958` -
the table, the scaling, the per-character storage, the two commands - is the original's.

### A fourth slip: the disease cure roll reads the poison level

FRMDUNM.BAS rolls to shake off poison and disease with what is meant to be the same expression, but
both copies subtract `Ailments(0)` - the **poison** level:

    ' poison
    l013C = Int(Rnd * 200) + ((CON + gearCON - 10) * 2) - (Ailments(0) * 2)
    If l013C > 185 Then cured Else Hits = Hits - 2
    ' disease
    l013C = Int(Rnd * 200) + ((CON + gearCON - 10) * 2) - (Ailments(0) * 2)
    If l013C > 195 Then cured Else Hits = Hits - 1

So a disease's own strength never affects how long it lasts, while being poisoned at the same time
makes it harder to shake. The port reproduces this. Measured over 4,000 runs, a disease lasts the
same ~49 steps at CON 10 whether its strength is 1, 5 or 10.

The formula also has hard floors that are easy to miss. The roll tops out at 199, so:

| | cure needs | impossible when |
|---|---|---|
| poison | roll + (CON-10)*2 - 2*strength > 185 | CON 6 and strength 5 or more |
| disease | roll + (CON-10)*2 - 2*poison > 195 | CON 6, always |

A low-constitution character genuinely **cannot** shake a strong poison, or any disease at all,
while below ground - the only cure is to reach the city. That is a consequence of the arithmetic
rather than a separate rule, and it is not a mistake, so it stands.

### A correction the source forced on the published identification page

`Rules.IdentifyOnPickup` came from the community's Item_Identifying_Formula and used the **dungeon
level you are standing on** to scale an item's difficulty. `fn12F0` uses the **item's own floor**:

    l03C0 = 25 + ((Log(item.DamageMod + 1) + .3#) * Log(item.LevelScale + 1) * item.Floor)
    If l03C0 <= skill Then
        If item.Floor <= 5 Then result = Mostly
    Else
        If (l03C0 - skill) >= 20 Then result = Barely

Those coincide only while you are finding items from the floor you are on, which is the usual case
and presumably why the page reads as it does. They part company as soon as a deep item turns up
shallow, or the reverse. The skill side also includes equipment stat modifiers, which the page
omits. Both are now taken from the source.


**A chest's gold was paid before the chest was opened - corrected 2026-09-25.** `sub0C53` ends a
won fight with

```
If Fight.TreasureClass = False Then
    l01D2 = monGetGold()
Else
    ...monShowChest
```

(MONSMOD.BAS:783-789), and the only other call of `monGetGold` is inside `monOpenChest` (3660).
So the gold of a fight that left a chest is *in* the chest: walk away and it is lost, and a trap
that kills pays nothing. The port paid it at victory whatever was left behind. `PayGold` is now
`monGetGold` alone, `AwardSpoils` calls it only when there is no chest, and opening the chest calls
it between the trap and the treasure, as the original orders them. `selfcheck` asserts all four
outcomes over 400 fights.

**What a deadly chest trap does, which is not what the port did.** After the trap, MONSMOD.BAS:3652
tests `Chars(m01BC).Hits < 1` with `m01BC` = 0 - the **leader's** hits - and calls
`mdrcharSetDead` on the **opener**. In a party those are different people: a leader the explosion
left on nought gets whoever opened the chest killed, and an opener the trap killed is not declared
dead there. Then `Exit Sub`, before `monGetGold` and before `Fight = l05AC` empties the fight
record, so the chest stays - untrapped now, `Fight.ChestTrap = False` at 3514 - with its gold and
items, for somebody else to open. All of that is reproduced. `mdrcharSetDead` writes nothing to
the message list (only the guild log's "died on level N"); the port had "The trap kills you." and
"You have died." there, which were its own and are gone.

A new test hook, `MORDOR_TEST_CHEST=1` with `MORDOR_TEST_FIGHT=1`, puts the level's likeliest
chest-dropper in front of a tester with 900 hits, so this can be watched: the Pseudo-Dragon left a
chest, the victory paid nothing, and opening it said "Your share is 62,183 Gold!".

**Switching and leaving say nothing - corrected 2026-09-25.** Nine sentences in the party keys were
this port's own, and three of the keys did something else as well.

- `n` is `fn1521` case 78 (MAINMOD2.BAS:3731-3739): `Me + 1`, back to 0 past the end, posted to the
  Party window's `lblCall`, which switches if that is not `Me` already (FRMGROUP.BAS:151). It does
  not skip the dead. The port picked the next *able* member and said "X is watching now.",
  "There is nobody else." or "Nobody else can act." - a wording its own comment admitted was
  untraced.
- ALT-1..4 is `fn15DF` (MAINMOD2.BAS:4222-4230): in a party, and only for a slot that exists, the
  same `lblCall` switch. An empty slot is ignored; "Nobody is in that slot.", "X steps up." and
  "Switched to X." are gone.
- ALT-s, "Switch to Character (updates all windows)", is `If gv1A82 <> Me Then sub1536 Me`
  (MAINMOD2.BAS:3416-3418). It does not move to anybody: it brings every window onto the character
  already in command. The port cycled to the next member. With one index kept here it is a repaint.
- `mdrmulSwitch` itself writes no line, and it redraws the chest for the new character
  (MULTIMOD.BAS:625-627), which the port now does. `monShowChest` draws nothing for a character
  with no hits (MONSMOD.BAS:4090), though the chest stays for the others.
- ALT-l and the Leave button are `sub159D` (MULTIMOD.BAS:739-758): the monster refusal in the
  dungeon, then "Leader cannot leave Party!" - which is also what a lone character hears, in
  place of the port's "You are alone." and "You are travelling alone." - then
  `MsgBox "Ask "X" to Leave?"` Yes/No, titled "Leave". Nothing is said after. The port's town copy
  had neither refusal, let the leader leave, and never asked; both copies announced "X leaves the
  party." with a new-leader line, and neither `mdrcharUnload` nor `sub1776` writes anything.

**A round is each character whole, then the next - corrected 2026-09-25.** `mdrmulInitiateCombatRound`
(MULTIMOD.BAS:511-539) walks the party once, and for each living character does their own action
(`sub1019`) and then their companions (`monCompanionAttack`) before moving on; the monsters go after
the whole party (`sub0A64`, 555). The loop starts with `If fn0EEF() = False Then Exit For`, so when a
companion kills the last monster the characters after its owner do not act that round at all. The
port ran every character and then every companion. A paralysed character's own turn is skipped
inside `sub1019`, but their companions are called separately and still fight - the port had skipped
both. `selfcheck` sets it up so the difference shows: the old order fails it on all 193 rounds.

**A chest can be opened in the middle of a fight - built 2026-09-25.** The help: "Chests that are
identified and not magically locked can also be opened during combat as they are during
non-combat, except that it usually takes a number of rounds and makes your character much easier
to hit." The port only offered the chest once the fight was over. Now:

- **The chest is drawn as the monsters appear** (`sub0A91` calls `monShowChest`, MONSMOD.BAS:728)
  - unless the character in command fails to spot it. With monsters standing, MONSMOD.BAS:4121-4129
  hides the picture, the trap guess and the gauge when
  `Abs(Log(DEX/4) + Log(DEX/4) * (disarm/100 - .5)) - Log(leadDEX/8) < .5`, on the character's
  own DEX without gear against the lead monster's. That hides it from most characters who are not
  thieves: the test hook's DEX 12 novice never saw one, and a DEX 18 level 20 thief did. Switching
  character redraws it for the new one (MULTIMOD.BAS:625-627).
- **`o` in a fight** (`fn1521` case 79, MAINMOD2.BAS:4063-4090) does nothing for the dead, the
  paralysed or a magically locked chest. Otherwise it reloads the orders of anybody holding none
  (`sub0BDE`, which also zeroes their counts), clears your own orders, and - only if the chest is
  on screen for you - starts your count, or stops it if it was running. The count is
  `CInt(10 - Log(disarm - 10) * 2)`, at least 2: ten rounds for a disarm chance of 11, two for 60
  and above. A chance of 10 or less is `Log` of nought or less, an error that fn1521's handler
  swallows, so the count never starts.
- **Each character's turn begins with their count** (MULTIMOD.BAS:517-531): one round off while
  there is gold in the fight, and at nought `monOpenChest` for *that* character, whoever is on
  screen - its trap, its disarm chance, its experience. With no gold the count is dropped.
  lblCharacterHit reads "Opening Chest (N round(s) to go)" for the character in command (sub0FE1,
  MONSMOD.BAS:4475-4486); when that character is not counting, the caption is cleared at the head
  of their turn, every round.
- **Easier to hit**: a monster's blow at a counting character, or at one of their companions,
  carries +0.75 on its damage modifier (MONSMOD.BAS:2907-2909). The Party window's Option column
  says "Open" (FRMGROUP.BAS:464-465).
- **What stops it**: Defend (for a character who can act), the cursor keys, `t`, Fight, space,
  and Shift and a number for a buffer that holds something - each `If gv1E7C(Me) > 0 Then
  gv1E7C(Me) = 0` (MAINMOD2.BAS:3594-3600, 3759-3763, 3946-3949, 4011-4016, 4040-4044, 4137-4145).
- **Opening empties the fight record** (`Fight = l05AC`, MONSMOD.BAS:3868), so the chest, its trap
  and its gold are gone and the victory that follows pays nothing further.

Two faults of the original ride along. `sub0BDE` zeroes the count of every character without an
order whenever it runs, and using an item in a fight runs it (MAINMOD2.BAS:6132) - so one party
member using a potion stops another's chest. Both are in: `Sub0BDE` drops the count of each
member without an order, and the item path runs it (DungeonWindow.axaml.cs `Sub0BDE`, called
after an item's spell engages, MAINMOD2.BAS:6131-6132). And the empty-buffer case says nothing at all
("If Chars(Me).M59B0(n) > -1", 4141): "Buffer N is empty." was this port's, and is gone.

**Built since:** a chest on an encounter that has not started fighting. The original rolls the
chest with the monsters and `o` at a waiting encounter starts the fight ("attempting to open a
monster's chest" is a hostile act, the help says); the port now shows the waiting group's chest
(`ShowWaitingChest`) and `o` engages it, `Foes(0).Engaged = mc0138` (MAINMOD2.BAS:4068-4069;
`OpenTheChest`).

`selfcheck` asserts the count's arithmetic against hand-worked values, that a count runs down and
opens once for the right character, that no gold drops it, that the caption is the watched
character's, and that counting makes the same dice hit harder and never softer. On screen with
`MORDOR_TEST_CHEST`: the chest beside the Pseudo-Dragon mid-fight, "Opening Chest (1 round(s) to
go)", "Open" in the Party window, and `o` doing nothing at a "Magically Locked" one.

**More of the port's own lines, removed 2026-09-25.** Each checked against the routine it stands
in for:

- "X: Fight." / "X: Defend." / "X: None." in the combat list whenever an order was given. Fight,
  Defend and space set `CombatOrders(Me)` and write nothing (MAINMOD2.BAS:3594-3602, 4011-4054).
- "X takes aim at Y." on a number key. The case is `CombatOrders(Me).M547A = n : monShowCursor`
  and no more (4131-4136).
- "You use the X." and "The X is used up." wherever an item was used. `sub15CD`'s only lines are
  "…the Item doesn't appear to do much of anything.", "…the Item is out of charges!", "Nothing
  Happens.", "Item must be Equipped to use!" and the ageing ones (6099-6194).
- "Buffer N is empty." An empty buffer is `If Chars(Me).M59B0(n) > -1` and nothing else (4141);
  a buffer pointing at an emptied pack slot reaches `sub15CD`'s silent `Exit Sub` (6096), and the
  port also stopped clearing the buffer there, which the original does not do.
- "Lost in the fight: …" at the end of a fight. Each destroyed item already has its own line as it
  goes (MONSMOD.BAS:3288-3315); the summary was extra.
- "You are paralyzed and cannot act!" and the PARAL sound when a paralysed character chose a spell.
  The original has no such line. Settled since: `cmdCast_Click` (FRMCHAR3.BAS:174-184) has no
  paralysis test, so a paralysed character's spell becomes the order and `mdrcharCheckCast` runs
  as for anyone; the port no longer refuses it (`CastInCombat`).

`selfcheck` now scans the code, comments excepted, for every line removed today and asserts none
has come back; it caught one copy left behind on its first run.

**Out of spell points, the wall, and the encounter itself - corrected 2026-09-25.**

- **A standing spell order with too few points** goes through `mdrcharCheckCast`
  (MAINMOD2.BAS:502-510): "Not enough spell points to cast "X"!" for the character the windows
  show, "Name doesn't have enough spell points to cast "X"!" (no quotes round the name) for
  anybody else, and then `sub102F caster, 1` (831-834) - the order becomes **Fight**, at the same
  group. The port wrote "X cannot keep casting Y." on a channel nothing displayed and dropped the
  order to None. The two shorter "Not enough spell points." fallbacks now use the original wording.
- **Walking into a wall** plays OUCH.WAV (`sub09DE mc0606`, 3897) and writes nothing. "You walk into
  a wall." was the port's.
- **No encounter is announced.** MONSMOD writes no line when monsters appear or when they attack -
  its only messages are deaths, drains, thefts, companions and errors - and the help's own picture
  of the dungeon window mid-fight (dmainw1.png) shows the skull, the portraits, the chest, the
  numbered groups and the red lblMonsterHit caption, nothing more. The port had "A Pseudo-Dragon
  attacks!" as the panel title, on the message bar and in the list; "X - not moving yet" and "You
  see X. It has not come for you yet." for a group that has not moved; "They watch you, and do not
  come." / "They have not come for you. Attack, or walk away."; and "(f to fight, or walk on)". All
  gone. The one line that is the original's - "N X offers to 'j'oin." - moved to the red caption,
  where MONSMOD.BAS:4343-4346 puts it. `EncounterGenerator.Describe` survives only for the probes to
  print.

**A facing square turns you without a word - corrected 2026-09-25.** MAZEMOD.BAS:1714-1729 sets
`Facing` for the four facing squares after `mdrmazPerceiveSquare`, and writes nothing itself;
anything said comes from perception. "An unseen force turns you to face north." was this port's.

**A damage spell works group by group and front to back, and says so in the original's words -
corrected 2026-09-25.** The damage branch of `mdrcharCastSpell` (MAINMOD2.BAS:1403-1601):

- It starts at the caster's group and covers `M5B28` groups (the port's `Damage1`), wrapping round
  the four slots and spending a step on every empty one it passes (`l028E`).
- In each group it takes up to `M5B1D` monsters (`AffectGroup`) **from the front, one at a time,
  and stops at the first survivor.** A Firebolt that fails to kill the front Orc never touches the
  three behind it. The port hit every monster the spell "covered" at once.
- Each victim is rolled separately. An all-or-nothing spell (effect 100 - Sleep, Nerve Blank,
  Dazzle and the rest) that would not kill does nothing and ends that group.
- The resistance is the group's monster's, by the spell's own resistance index (`M5B5B`); the
  port had been choosing a column by spell category. A magic-immune monster (property bit 2)
  takes nothing, and so does one with bit 3 from a kind-7 spell.
- One caption per group on lblCharacterHit, held half a second for the watched character (1567):
  "You " + the verb from gv0972$ (by `M5B12`, the port's `AffectMonster`) + the count in words from
  gv0846$ ("One" … "Ten", then "A Lot"), then " and damaged one for N" for the survivor it stopped
  at; "You damaged one for N" with no kills; and "Failed" when nothing happened - which also puts
  the caster on **Fight** (`sub102F caster, 1`). The prefix is written only for the character in
  command; anybody else's captions are never seen. The port wrote "X casts Firebolt: 12 to the
  Orc, killing it." into the list.

`selfcheck` asserts the field mapping, three kills in one caption, a front survivor shielding the
two behind it, one caption per group across two groups, and Failed plus the Fight order against
a magic-immune monster. On screen: "You Slept One!".

**A group spell opened on a dead group and lost a group - corrected 2026-10-01.** The loop above
charges `l028E` for every slot it passes, empty or not, and `sub1019` (MONSMOD.BAS:297-305) makes
sure it never opens on an empty one: before a character acts, a target of -1, or one on a group
with nobody left, is replaced with `fn0DCD(-1)`, the first group standing. The port's `CastDamage`
opened on group 0 whenever the order had no target - which, for everyone but the character in
command, it never has - and on the aimed group even after it had fallen. The skip over the dead
group spent a step the spell then lacked: once the first group was down, a two-group Firebolt
reached one group and a three-group spell two. The character in command was not spared: the
window moves their target off a dead group only when it redraws the monster list, and `PlayRound`
redraws on their own lines and at the end of the round, not on another member's. A party member
or companion acting before them in the round who killed the aimed group left their Firebolt to
open on it. Melee had always had the fallback (`FoeOf`); the spell path now resolves its opening
group the same way, at the cast, whatever the window has drawn.

**The mark walks with the spell - built 2026-10-03.** Inside the same loop, each group reached
becomes the caster's target - "CombatOrders(pv01A6).M547A = l0286" (1456) - and for the character
in command lblCurrentGroup is moved beside that group's row (1457-1460) while its caption holds
for the half second; after the loop the target goes back to the group aimed at, or to fn0DCD(-1),
the first standing, if that one is gone (1582-1586), the caption is cleared (1571) and monShowCursor
redraws the mark there (1590). The port's mark is drawn from the order's target at each refresh
and the cast never moved it, so it sat on the aimed group through every caption and stayed there.
Each caption now carries the group it was written for (`CombatLine.AimAt`, from `LastCaptionAims`);
`PlayRound` moves the order's target there as the caption shows, and an empty caption line after
the last one clears lblCharacterHit and carries the target the loop left (`LastCastAim`). One
difference stays: the original's label floats beside a row that monShowMonsters has just hidden
when the spell emptied that group; the port's list has no row to put a mark beside, so its
existing fallback shows the mark on the first group standing for that half second. `selfcheck` casts with the first group
dead, with no target and with the target left on it, and expects both live groups reached.

**A room re-entered mid-fight keeps its emptied slots for a group spell - corrected 2026-10-18.**
A room read back with a standing record makes members only of its standing slots (`Finish` skips
GroupCount 0), and the cast's groups were the runs of the list, so an emptied slot 1 between 0 and
2 vanished and a three-group spell over slots 0, 2 and 3 reached slot 3. The original walks the
four `Foes()` slots and spends a step (l028E) on each with GroupCount below 1 (MAINMOD2.BAS:1426-1448),
so it stops at slot 2. `CombatMonster.BySlot` puts each run at its slot (members carry it as
`Group`) with an empty list for a gap; a list built by hand, or a turned companion (Group -1),
takes the slot after the run before it as before. Still the port's: a companion that turns takes
the first slot at GroupCount 0 in the original (MONSMOD.BAS:557), here the one after the last
group; and the window numbers its rows by run, not by slot.

The heal, cure and charm paths of the same routine had kept lines of the port's own ("X casts
Heal on Y, restoring N hits.", "…the X resists."); those are gone since, and the charm is the next
entry. Whom a heal or cure from the timed round reaches is settled too: nobody, because the round
never casts one. mdrcharCheckCast refuses a non-combat spell in an engaged fight (MAINMOD2.BAS:512-519,
re-run first by the round's mdrcharCastSpell at 831) and the member fights instead, so the engine no
longer picks a patient (`CombatEngine.NonCombatInFight`, Combat.cs; see "Where a non-combat spell
went" below). "Cast on Who?" is asked only out of a fight - see "A heal asks who it is for" above.

**A companion casts - built 2026-09-25.** MONSMOD.BAS:1038-1083. A companion whose monster has
spells, on a square without anti-magic (bit 18 only - the extinguisher, which stops a *monster*
casting, does not stop a companion), asks `fn0FF5` for a spell - the same chooser monsters use.
Only one of an owner's companions casts in a round (`l01EA`), and one that casts does not also
strike.

On a mishap (`fn0D64`'s spell odds: 1.5 × the base, clamped 1..40) the victim takes it - "You were
accidently damaged for N!" if they are on screen - and their companions in slots 0 to the spell's
`M5B1D` each take their own roll; the line is `"X: Companion #1 accidently cast "Spell" on
"Y"!"`. Two faults, both reproduced:

- The " and his companions" suffix is reset on **every pass** of the companion loop (1063), so it
  reflects only the last slot visited. Blue Flame's loop runs to slot 2; a victim whose only
  companion is in slot 0 is named alone although the companion was hit - `selfcheck` sees it on
  every one of 55 mishaps.
- `monGetSpellHits` for a companion casting at monsters tests magic immunity on **the companion
  itself** (2556): an immune companion's spells do nothing, and an immune monster takes them.

Otherwise the companion casts through `mdrcharCastSpell` on its owner's behalf, at its guild
level over 6. With a companion slot passed, that routine never takes its non-combat branch (856),
so **every** spell goes at the monsters as damage - a heal included, and since a heal's `M5B1D` is
0 it reaches nobody: "Comp. #1 failed in casting "Minor Heal"!". The captions are the damage
branch's with "Comp. #N " for "You ", and "failed in casting" where a character would get
"Failed" and the Fight order.

**A companion's cast takes a charge off its owner's item - corrected 2026-10-18.** With a companion
slot passed, `mdrcharCastSpell` skips mdrcharCheckCast (829-835) but not the rest of its head: it
draws `l01D0 = Int(Rnd * 100)` (840), which nothing reads, and then, when the owner's M5766 holds a
pack slot - an item's order, or the slot an own spell left there - takes a charge off it, or with
none left says "Your item has run out of charges!" / "The item "X" was using ran out of charges!",
runs sub102F owner, -1 and casts nothing (841-853). The port's orders do carry the slot now
(`ItemOrders`), so the companion's cast goes through the same `ItemCharge` as the owner's. The
draw at 840 was missing from every cast in the engine, and is made now - after the check and before
the charge - for the round's own casts and item casts as well (`CastSpellDraw`), and for the casts
outside a fight (`CastSpellHere`: the Cast button, FRMCHAR3.BAS:186; a buffer key, MAINMOD2.BAS:4159;
an item's Use, 6140; and a mod's cast, the Kaizen mod's included, which goes the same way). The
run-out's sub102F owner, -1 (850) takes its first branch when the owner has no spell queued and Att
is 0 or 1 - Defend (MULTIMOD.BAS:470-474) - and Fight otherwise (476-478): `CombatOrder.Sub102FMinusOne`,
which the anti-magic refusal of a cast by hand uses too. The port had always put the owner on Fight.

`selfcheck`: "Comp. #1" captions and nothing else from a lone owner's caster; never two casters in
a round (a mutant without the gate fails 172 times); the heal failing; the mishap line's missing
suffix. On screen the rounds with caster companions (the new `MORDOR_TEST_PETS=caster`) run, but no
capture happened to catch a companion's own caption - the fights end in a second.

**Where a non-combat spell went - corrected 2026-09-25.** Heals, cures, the four states,
resistances, Fate and Set Sanctuary all end the same way in `mdrcharCastSpell`
(MAINMOD2.BAS:1358-1391): one line saying where the spell landed - `"Heal" thrown on yourself.`,
`"Ann" threw "Heal" on herself.`, `"Ann" threw "Heal" on "Bob".`, `"Heal" thrown on Companion #1.`,
`"Heal" thrown on the party.`, or `"Sanctuary" thrown.` It goes to the Messages window
(`sub085B`). `SpellThrown` carries it. What it replaced was this port's: "You cast X. …", "The
poison leaves your body.", "You are not poisoned." (the original makes no such check), "Ann is
warded.", "… is already that well protected." (a resist at 95 is left alone in silence), "Ann:
Levitating Invisible.", "Fate turns: N hits, M spell points.", "This place is your sanctuary.",
and in the engine "X casts Heal on Y, restoring N hits." and "X lacks the spell points for Y
(needs N)." - with a heal halved on a diseased character, which is in no formula.

Who is asked follows the cases (917-1115): SPELLHEAL (party and companions) for the heals and Cure
Paralysis; SPELL (party only) for the cures, the states and the resistances; nobody for a
party-wide form (effect + 200 - the heal, Levitate, Invisible, Protect and the resistances have
one; See Invisible and the cures do not), Fate or Set Sanctuary. The spell points come off after
the effect (1355-1357), so a cancelled box costs nothing.

Three faults of the original, reproduced:
- **Cure Paralysis frees the caster, not the one picked** (925-929: `l01C8 = pv01A6 :
  sub0D1D pv01A6, 1, 0`). The pick only decides whom the line names.
- **Fate names the party's first character.** The case never sets `l01B6`, which is therefore 0;
  in a party the line reads `"Bob" threw "Fate" on "Ann"` whoever cast it.
- **Fate draws a number it never uses** - `l031E = Int(Rnd * 3)` at the head of `fateReroll`
  (1913) - before rerolling hits and spell points.

**A heal or cure in an engaged fight is refused, not aimed - corrected 2026-10-18.** This was "not
settled": a heal or cure reached from the round or from an item in a fight picked its own patient
(the first who needed the cure, the most hurt for a heal). The original asks nobody there, because
the box is never reached: fn149E calls every Heal-class spell and every spell of nought damage
non-combat (MAINMOD2.BAS:2908-2921; Cure Paralysis and Teleport are the "either" two), and
mdrcharCheckCast refuses a non-combat spell while monsters are engaged - "X is a Non-Combat
Spell!" or ", Name." (512-519) - with no exemption for an item's level. Every way in does that
check: the Cast button and a buffer key queue a spell only after it (FRMCHAR3.BAS:171-185,
MAINMOD2.BAS:4150-4156), the Combat Setting through it (MULTIMOD.BAS:176-182), an item through it
(MAINMOD2.BAS:6129-6130), and the round's mdrcharCastSpell runs it again first (831, from
MONSMOD.BAS:313-315), with "sub102F caster, 1" after a refusal (832): the member fights. "Cast on
Who?" (917-941) belongs to the non-combat branch, which a non-combat spell reaches only with no
engaged fight - with nothing here, or with monsters standing peaced - and there the port already
asks (CastSpellHere), companions and cancel included. So the engine's own pick is gone: the round
refuses such an order before the item's charge (841) and turns it to Fight
(`CombatEngine.RoundCheckCast`), and a direct cast is refused the same way. The round asks the rest of
mdrcharCheckCast every cast as well, in its order: not knowing the spell (437-455), the spell level
(457-481), a stat (482-497) - skipped for an item with a level of its own - the points (502-510) and an
anti-magic or extinguisher square (520-526), whose map mark the window makes; each refused before the
draw at 840, then Fight (832). The port had asked only the points.

**The square is the one stood on now - corrected 2026-10-19.** Each of those square tests reads
`squareWalls(squareIndex(Chars(...)))` as it is asked: the round's cast on the caster's square
(MAINMOD2.BAS:521), a companion's cast on its owner's (MONSMOD.BAS:1039), a monster's on the
character it attacks (2841). A fight belongs to its area, not its square, so the party can step
onto an anti-magic or extinguisher square of the same area while it goes on. The port read the
square once, when the fight started, and kept that answer to the end of it: a fight begun on an
anti-magic square refused every cast after the party had walked off it, and one begun beside it
let them all through on it. `SquareTests` now reads the square before each round and each
monsters' turn (the leave and arrival rounds included).

`selfcheck` asserts every form of the line, the two Fate faults and Cure Paralysis's; the guard
against invented lines now includes the retired ones. On screen: `"Feather Essense" thrown on
yourself.`, with the points spent.

**A charm is a damage spell that keeps what it would have killed - corrected 2026-09-25.** The port
had its own `CastCharm`: a success chance built from the charm's *name* ("Dominate…" 3,
"Control…" 2), the caster's charisma, the spell level and the monster's resistances, on the first
monster of a kind also read from the name, with "X casts Y. The Z is charmed and joins you!", "…
but the Z resists." and "There is nothing here that Y can affect." None of that is the original.

In `mdrcharCastSpell` a charm (`Kind` 7) runs the damage branch:

- **No room, no cast.** With no free companion slot the spell is refused before anything is
  spent: `"Charm Humanoid" - No room for more Companions!` (1410-1416). The slots counted are the
  owner's even when a companion casts.
- Each victim's damage is rolled as for any damage spell - the class-only effects (75-99) decide
  which creatures it bites on - and **a charm takes only a creature its damage would kill; one it
  would not is left untouched** (1473-1477). The front survivor stops it, as for any spell.
- What it would have killed **joins instead** (fn1163, 383-405): with the hits it had, its rolled
  maximum (`Foes().MaxHits` - this port now keeps it as `CombatMonster.MaxHits`), bound at the
  **spell's level**, and identified by `fn1272`. The port had bound it by the Confinement's
  purchase rule (5 + floor, at most 10) and capped its hits at the creature's base.
- The caption is `"You charmed One!"` - "You charmed " is literal, with no other prefix
  (1527-1528); nothing charmed is "Failed!" and the Fight order, as for any spell.

Two faults reproduced: a slot that fills in the middle of a cast leaves the creature that would
have joined **gone all the same** (the count is taken back, 1515); and when a **companion** casts a
charm, the joining is skipped entirely (it is inside `If p01AC = -1`) - the creature vanishes and
the caption still says it was charmed.

Tracked since 2026-10-18: the creature's alignment, which fn1163 copies from the foe
(`p00D6.Alignment`, MAINMOD2.BAS:392), so the companion is penned at it when sold.

`selfcheck`: a one-hit humanoid charmed with its hits, rolled maximum and the spell's level as
binding, and out of the fight; one it cannot kill untouched and "Failed!"; the wrong class
"Failed!"; four companions already, refused and untouched. Not checked on screen - a charm cannot
be picked from the combat spell box with key presses alone.

**Joining - monsters and characters - says nothing - corrected 2026-09-25.**

- **Monsters** (`j`, MAINMOD2.BAS:4095-4125): with no free slot, "Join - No room for more
  Companions!" and the offer stands. Otherwise the offer is taken and **nothing is said**; the red
  caption that carried it is hidden. The port had "N X join you." Every joiner is made by
  `fn1163(Me, Foes(0), Foes(0).Hits, -10)` in a loop that never loads the next member, so **all of
  them arrive with the front one's hits and rolled maximum** - reproduced. Only the first group
  can join (`Foes(0).GroupCount`); the port had been taking from the back of the whole encounter,
  which with more than one group reached into the next. The offer itself, "N X offers to 'j'oin.",
  is on the red caption (MONSMOD.BAS:4343-4346), as the encounter entry above says.
- **Characters** (`a`, MAINMOD2.BAS:3440-3520): a join that works writes nothing - "X joins you."
  was the port's, in both windows. A wrong password is `MsgBox "Incorrect Password!", 64,
  "SORRY!"` (3517), a box, not a message line (both windows now use the box; the town's
  Parties panel had shown it as text). Joining in town restores the newcomer's hits and spell
  points (3503-3510), which the port had not done. The town's "There is nobody else in the city."
  is now frmInteract's own "No other characters in the area!", and its "Choose someone." is gone.

`selfcheck`: joiners all made from the front one, bound at -10, only the first group, silent,
and the no-room refusal with the offer kept. On screen: the offer on the red caption, and after
`j` the caption cleared with nothing written.

**Using and equipping an unknown item; nothing selected; SHIFT-F6 - corrected 2026-09-25.**

- **No identification test.** Neither the equip routine (`sub0F74`, MAINMOD2.BAS:1712-1820) nor the
  use routine (`sub15CD`, 6081-6240) asks how well you know the item. The port refused with "You
  don't know what that is. The Seer in town can identify it." in both windows; that is gone, so an
  unidentified item can be put on or used, cursed or not, exactly as in the original.
- **Nothing selected does nothing.** The pack buttons leave silently on an empty slot
  (`If m001A > -1`, FRMCHAR3.BAS:206; `p0852 < 0 ... Exit Sub` in sub15CD). "Select an item in your
  pack.", "Select an item first.", "Select an item.", "Select an item in the list." and "Select an
  item to buy." were the port's.
- **SHIFT-F6** says "BETA: Writing Character Statistics to File..." (5231). The port had "Written to
  X." and "Could not write CHARSTAT.TXT.". The original's closing line never appears: the ability
  loop reuses the "write the file" flag `p06E4` for each ability's chance, and the footer is
  guarded by `If p06E4 = True`. The report is now the original's: `CharStatFile.Paint` is
  `sub09B8`'s sequence and `Compose` what the file receives. The version it opens with is "1.1": `gc0186` is
  one of the dropped constants, but the game stamps its data files with it on every start
  (MAINMOD.BAS:129-147) and every shipped MDATA file carries "1.1", as does the help's title.

**Equip, drop, bank, swaps and arrivals are silent when they work - corrected 2026-09-25.** A scan
of every literal the port writes to the player, against the decompiled source and the help,
turned up another run of the port's own lines. Each was checked against its routine:

- **Equip and unequip** (`sub0F74`, MAINMOD2.BAS:1712-1820) speak only to refuse - "Cannot
  unequip!  Item is cursed!", "<stat> is too low!", "Your stats are too low for the item to
  properly attach!", "You are too weak to equip this item!". Gone: "You equip the X.", "You
  unequip the X.", "…and feel a chill. It is cursed and will not come off.", "The X is not
  something you wear." (the routine leaves without a word), and in the town store two alignment
  warnings ("The X is Evil and you are Good. Wearing it will count against you…").
- **Drop** (FRMCHAR3.BAS:227-246) is silent after its "Drop the X?" box: "You drop the X. It is
  gone." is gone.
- **A deposit in the bank** (FRMCITYB.BAS:175-200) is silent: "X is in the vault." is gone.
- **Dragging one pack slot onto another** is silent (only the `s` key says "Item swap
  completed!"): "Slot #a and Slot #b change places." is gone.
- **Entering the dungeon, a fight ending, walking off one**: no message in any of the routines.
  "You enter the dungeon.", "The fight is over." and "You leave them behind." are gone; the square's
  own description (stairs, chute) that followed them stays, being the original's.

Left as the port's own then, and all gone since (none is in the source any more): "Your
pack is full." on the bank's take-back button (the original withdraws by dragging onto a slot, so
it never meets a full pack); the Seer, Confinement and Store confirmations ("The X is bound to you
at level N. (fee)", "They take the X and pay you N gold." and the like); "Asmodeus stands before
you.  (f to fight)"; and the prompts of this port's own panels ("Pick a spell first.").

The invented-line guard in `selfcheck` covers the removed ones, and now ignores trailing comments
on code lines as well as comment lines.

**The store and the Confinement speak in boxes, and only to refuse - corrected 2026-09-25.**

The store (`FRMCTYS`) and the companion shop (`FRMCITYC`) put every refusal in a `MsgBox` titled
by its service - "Not enough gold!" under "BUY ITEM", "Identify Item", "UNCURSE ITEM", "SORRY!",
"Buy Companion", "Identify Companion", "Bind Companion"; "No free slots!"; "Item is Equipped and
Cursed!" under "SELL ITEM"; "Cannot realign Equipped item!" under "SORRY!" - and say nothing when
a service works, with three exceptions that are the original's own lines: "Slot #N now contains
your X." after buying an item (FRMCTYS.BAS:109, N the pack index plus one), "Item has been
separated from you." after uncursing (319, no fee added), and "Companion #N is now your X." after
buying a companion (FRMCITYC.BAS:172). Realigning and binding put the cost in a Yes/No box first -
"Cost to realign is N. / Do you wish to pay?" and "Cost to bind companion to a binding level of L is
N. / Do you wish to pay?".

Gone, all the port's: "X costs N gold; you have M between your purse and the bank.", "It is a Good
X, completely identified.", "You learn a little more: …", "The X is realigned to Y. (fee)", "Sold
the X for N gold.", "The shopkeeper squints at it. …", "You still hold X [n].", "Too little of it
was left to put back on the shelf.", "No Good X in stock." and "The store has no more of those."
(the original disables Buy when a shelf is empty), "It is opposed to your alignment; …" after a
buy, "Showing everything the store has." / "Showing items matching …", "You hold X [n]." after a
combine; and at the Confinement "The X is yours for N gold.", "They take the X and pay you N
gold.", "You learn a little more of the X.", "The X is now known completely.", "You already know
the X completely.", "The X is realigned to Y.", "The X is bound to you at level N.", "Select a
creature." and "Select a companion.". A bought companion is now nameless ("Companion #N") as
FRMCITYC.BAS:126 has it; the port had given it the breed's name.

Two faults reproduced: **neither realign tests the alignment the thing already has** - choosing
the same one asks the fee and takes it for no change (FRMCTYS.BAS:204-220, FRMCITYC.BAS:246-262);
the port had refused with "The X is already Good.". And the realign fee is a `Long`, so the
`.72 * Price` is rounded, not truncated: a price of 4 costs 3 to realign, not 2.

`selfcheck` asserts the rounding and the same-alignment charge; the invented-line guard covers
every removed line. The dialogs are not checked on screen - the store's buttons sit below the
automap in the test layout.

**Your own body at the morgue is its own routine - corrected 2026-09-25.** The port raised a
character who died in town through `fn16B5` (MAINMOD2.BAS:330-380), the routine that runs when
somebody *else* has arranged a raise. The original has a separate path for your own body, in
`sub0930` (3069-3118):

- The offer - "A small fee of N is required for your Resurrection." with the guild's offer or "Do
  you wish to pay?", and "Chance of success is N%." - is a Yes/No box titled **"Welcome to the
  morgue!"**; the port showed it as panel text. A short purse is "Not Enough Gold!" in a SORRY! box
  (the port added "(you have N between purse and bank)"), and "Raise Failed!" / "Success!" are boxes
  titled "Raise Info". "The Warriors settle the bill." after a raise the guild paid for was the
  port's.
- Its Constitution line is **"MORGUE: You lost a point of Constitution during the raise."** -
  "You", and spelled right - where fn16B5's, for a raise arranged at the counter, is "MORGUE: "Ann"
  lost a point of Constution during the raise." Both are kept, each where it belongs.
- It then **lifts Constitution back to the race's minimum** (3116-3118), which fn16B5 does not; a
  counter raise can leave a character a point under.
- Still in the dungeon, the town shows the dead character sub0930's own label, "You are Dead.  You
  may be resurrected by the morgue or a character, wait for rescue, or run another character.", in
  place of a paragraph of the port's.

`selfcheck` asserts both lines and the floor against its absence. Not checked on screen: no test
hook puts a dead character in town.

**The map spells say what the original says - corrected 2026-09-25.** Every spell cast from the
map (`CastHere`) had "You cast X. " in front of its result, which the original never writes, and
several results were the port's own. Against MAINMOD2.BAS's cases:

- **Depth Perception** "You are on level N." (1262), **Find Direction** "You are facing North."
  (1271), **Detect Rock** "The square ahead is Rock." / "The ahead square is not Rock."
  (1237/1257), **Soul Search** and **Retrieve Soul** their own lines (1289-1328) - each with nothing
  before it and nothing after (the cases set `l01BA`). Soul Search's direction line has **two
  spaces** before "and down" when you face North (1296) and one otherwise; reproduced.
- **Ethereal Portal** into rock is "You walked into solid rock!" and death, nothing more; through
  a wall it is the closing line, `"Ethereal Portal" thrown.` (l01B6 = -1). The port had "You step
  through the wall."
- **Sanctuary** and the **chest charms** end `"Sanctuary" thrown.` / `"Charm of Opening" thrown.`;
  the port had "You are back at your sanctuary, 28 days older.", "The lock holds." and "The lock
  springs open.".
- **Raise spells** refuse first if you are carrying *any* character - `"Ann", drop who you're
  carrying to raise the dead.` (1062-1066). The port had used the join path's narrower "Drop the
  character before you raise them!", which only applies to the body being raised. A companion's
  raise says "Raise Failed!" or "Raise Was Successful!", puts down whatever companion you carried,
  and closes `"Raise Dead" thrown on Companion #1.`; a character's closes `"Raise Dead" thrown on
  "Ann".`.
- **Control, Restrain, Domination** ask "Cast on Which Companion?" over frmInteract's COMP list -
  numbered buttons for living companions whose binding is not 0, or "No bound companions to
  select!" (FRMINTER.BAS:413-427) - and say `"Control" raised the Bind Level of companion #3 to N.`
  (by slot, lower case) or `"Control" had no effect!`. **Life Domination** says `Companion #1 is now
  bound to you for life!`. The port chose the least-bound companion itself, named it by its
  creature name, and refused with "You have no companion to bind.".

Then still the port's, built since: which dead character or companion a raise spell reaches - the
original asks ("Cast on Who?", SPELLRAISE, MAINMOD2.BAS:1067), and so does the port now, over the
bodies and the companion buttons - and refusing a raise with "Cannot raise character in Combat!",
which is the join path's line and is now said only there; a raise in a fight is
`mdrcharCheckCast`'s business. `selfcheck` asserts the binding lines; the invented-line guard covers
the retired ones. On screen: "You are on level 1.".

**Pits, stairs and the ailment clock in the original's words - corrected 2026-09-25.**

- **A pit** is "You have fallen into a pit for N hits." (MAZEMOD.BAS:1766, `SquareInfo` already
  had it) or "You are hovering over a pit."; the port wrote "You fall into a pit and take N
  damage!" and, when it killed, "... You have died." - a fatal fall is the fall's line and the
  death, nothing more.
- **Stairs** say nothing themselves: what you read on arrival is the new square's own label,
  "There are Stairs Up here." (MAZEMOD.BAS:1780). "You descend to level N.", "You ascend to level
  N." and, pressing `t` where there are none, "There are no stairs here." were the port's (the `t`
  case, MAINMOD2.BAS:3946-4010, has no such line).
- **The ailment clock** (FRMDUNM.BAS:534-580) cures poison and disease with a sound and nothing
  else, and a character it kills is `mdrcharSetDead` and `sub0930` without a line. "Kord: The
  poison passes.", "The disease passes." and "The sickness finishes them." were the port's.

**Arriving in town says what FRMCTYM says - corrected 2026-09-25.** `Form_Load` of the city
(FRMCTYM.BAS:70-128) runs through the party and, for each member, calls `mdrcharAddAge`'s rest -
whose only line is "You rested for 1 week(s) and 2 day(s)." (or `"Ann" rested for ...` in a party),
and nothing when no time passed (MAINMOD2.BAS:6392-6399) - and then, if that member carries a
*character*, leaves the body: "You are no longer carrying "Bob"." / `"Ann" is no longer carrying
"Bob".` (124-126). A carried companion stays carried.

The port had "Welcome to MORDOR!" (in no routine - the original's "Welcome to Mordor 1.1!" is the
first-run box in FRMMENU, a different thing), "Your wounds are tended (5 → 15 hits), the poison and
disease purged from you. 2 weeks pass; you are now 17.", and on the morgue page "You are no longer
carrying "Bob". The keepers take the body." - dropping a carried companion as well - plus "No
bodies of yours here. Stay alive." for a living visitor. All gone.

A fault reproduced: the store and the Confinement pens are replenished by **the first
character's** rest only (`If p088E = m00A6`, 6384-6389); the port had used the longest stay in the
party.

Also silent now, each checked: taking an item back from the bank (only the re-identify line,
FRMCHAR3.BAS:549, speaks - "You take it back." and "You take back the X." were the port's), and
cleaning the guild log after its two boxes (FRMGLDL.BAS:19-126 - "There was nothing to clean." and
"N old entries removed." were the port's).

**Creating a character refuses in the original's boxes - corrected 2026-09-25.** `cmdSave_Click`
(CHARCREA.BAS:128-181) checks, each refusal a box: "No Name Defined!" (NO NAME!), "The character
'|' is not allowed in a Character name." (Sorry!), "No Sex Defined!", "No Alignment Defined!",
`Name "Ann" already exists! / Try another.` (NAME ALREADY EXISTS!); then **asks** "You still have
points left.  Save Anyway?" (POINTS) when points are unspent; then the password twice, **again
and again until the two match** ("Passwords don't match!" in an ERROR box each time), and for an
empty one "You have chosen no password. / Is this okay?" (NO PASSWORD). A failed save is "Error
saving character to ...!". The port wrote "Enter a name.", "Choose an alignment." and "A character
called Ann already exists. Only one may hold a name." to a label, had no '|' rule and no points
question, gave up on a mismatched password, and never asked about an empty one.

The Tutorial button opens the help at its Tutorial topic, as `cmdTutorial_Click` (198-205) opens the
help file; the port had a popup of five sentences of its own. A small `OriginalBox` helper now gives
windows without their own a titled OK or Yes/No box. On screen: Save Character with no name puts up
"NO NAME!" / "No Name Defined!".

Left unsettled: what a dead character in town may do. The original city form has no dead check at
all (its Dungeon button just sets level 1), and the rest lives in routines not yet traced, so the
port's "You are dead. Visit the Morgue to be revived first." and "You are dead. The Morgue can
help." stand, marked here as the port's.

**A dead character in town - settled 2026-09-25.** Traced: `sub14E0` (MAINMOD2.BAS:3166-3182)
sends a dead character to the **dungeon** window whatever the level, never the town, and calls
`sub0930`, which at level 0 puts the morgue's "Welcome to the morgue!" box up at once. Declined, the
character is left on the dead screen - lblDead, the label "You are Dead.  You may be resurrected by
the morgue or a character, wait for rescue, or run another character." and cmdWait captioned
"Visit the Morgue" (3044-3051), which puts the box up again.

This port's dungeon window cannot stand on level 0, so a dead character still arrives in the town,
but now sees what the original shows: the box on arrival, and if it is declined the label and a
"Visit the Morgue" button on the morgue page, where the port had shown the offer as text beside a
"Pay N gold" / "Accept the guild's offer" button. The Dungeon button takes a dead character back
to that page, silently; "You are dead. Visit the Morgue to be revived first." and "You are dead. The
Morgue can help." were the port's and are gone. That a dead character could still open the town's
other services was the remaining difference; each service now sends the dead back to that page
(`if (_ch.IsDead) { ShowDead(); return; }`, as sub14E0 never shows them the town,
MAINMOD2.BAS:3166-3180) - the page is a window of its own, with the town not shown beside it (see the dead screen
entries). A `MORDOR_TEST_DEAD=1` hook (with
`MORDOR_TEST=city`) arrives dead; on screen the box and then the dead label are as described.

**The Char page and CHARSTAT.TXT are the original's - corrected 2026-09-25.** `mdrcharShowInfo` case
`mc0598` (MAINMOD2.BAS:5224-5334) paints the page with `sub09B8`, one piece at a row and column.
The column is counted in the font's **widest** character (`M37C0`, the seventh field of the 16-bit
TEXTMETRIC, tmMaxCharWidth) and the row, less one, in its height. The pieces are Kills (formatNumber),
Died, Comp, Mon Quests, Item Quests, Guild/Stats/Item A/D, Time, Date, Play Time ("###,###.00 Hrs",
so ".00 Hrs" at first), Created on, "Stats Nat(Mod)" with each stat bold once it reaches the race
maximum plus five, Total Exp. (every guild's) and Total Gold (hand and bank). The port showed its own
table of age, kills, deaths, creatures and items known, and natural against modified stats. It now
draws those pieces where the original does, in a proportional face as bm13.png shows. Shift-F6
appends the same pieces to CHARSTAT.TXT: a Tab before anything not bold, with the file-only ones in
between ("  VERSION: 1.1", CHARACTER, ITEM INFO, BANK ITEM INFO, Total A/D, GUILD INFO with each
guild's Single A/D through `CStr`, "Abilities (Lead Guild)"). The footer is still never written,
and Shift-F6 now leaves the Char page showing (fn1521, 3553-3555).

The page needed counts the port did not keep, so `M5906` is now filled as the original fills it:
- **Kills** go to the killer alone: by blow (MONSMOD.BAS:475) or by the character's own spell
  (MAINMOD2.BAS:1507), a charmed creature included. The port had given every party member every
  kill of a won fight.
- **Complications** count at raiseComplications (1654).
- **Item quests** count when the guild takes the item (FRMCTYG.BAS:265).
- **Monster quests** are settled by `sub0CF3` (1621-1639) **at the kill**, for the current guild
  only, by the character's blow or spell or a companion's breath, spit, blow, stoning or shock, and
  whether or not the fight is won. It says "You have completed your quest!", or in a party
  `"Name" completed his/her quest!`. The port marked the quest done at the end of a won fight, for
  any guild, silently.
- **Creation date** is set by CHARCREA.BAS:248.
- **Play time** is `sub16F8` (6004-6023), kept with its faults: 86340 seconds to the day across
  midnight, and an `Exit Sub` that starts one unstarted member per call.
- **Play time, the music and the effects stop while the game is minimised - corrected
  2026-10-18.** `Settings.Paused` is set when frmMain is minimised and cleared when it comes back
  (FRMMAIN.BAS:202-226). While it is set `sub16F8` adds nothing but still moves every member's
  start on to now (MAINMOD2.BAS:6012-6019), so the time away is never counted, even afterwards; the
  music is stopped and closed (207-209), `timMusic` leaves it alone (296), and with Sound FX on
  `WaveMixActivate(gv20C8, False)` gives up the wave device (210-212). Coming back, `sub1A50`
  starts the music again (221) and the device is taken back (222-224). The port counted on and
  played on ("the port has no pause"); now `FramePause` holds the flag, the menu and the town frame
  set it (`FramePauseWatch`), `PlayClock` and `Audio` honour it. Two notes: the restore branch
  tests for *maximised* (`mc00E4 = 2`), which is how a frame designed maximised comes back - the
  binary FRMMAIN.FRM has a byte of 2 after its position, read tentatively as that - while the
  port's frame opens at its design size and comes back Normal, so the port treats any return from
  minimised as that branch, as the combat and ailment clocks here already did. And sub1A50 picks
  a track afresh with Rnd (MUSIC.BAS:79-97); the port has one tune per place, which starts again
  from the top. Selfcheck "minimised, the game pauses".

Characters saved before this carry nought for all of these, and a creation date of 12/30/1899.

**The Stats page shows what its labels show - corrected 2026-09-25.** FRMCHAR1.BAS:128-172 loads
every `lblStatInfo` and makes it visible, "Spells" and "Spell Level" included, so a character with
no spell points still reads "Spells 0/0". The port had left those two lines off for non-casters.
Page 11 of `mdrcharStats` (MAINMOD2.BAS:5623-5630) shows `Str$(Stats + StatMods)` with no floor,
bold when the natural stat reaches the race maximum plus five: the bold 36, 24, 26, 24 of bm13.png.
The port had used the floored value, in plain type. "You have lost track of where you are." at the
foot of the page was the port's own and is gone. The citation above now names `mdrcharShowInfo`
(4768), the routine that really holds case `mc0598`; `mdrcharSetInfo` only picks the tab.

**The Stats page is the form's labels, each in its own font - corrected 2026-10-14.** The port drew
the page as one text in lblStat's Courier New, its columns lined up in spaces, so with the Options
fonts every caption ("Age", "Spells", the stat names), the race and title lines and the states came
out in the Pro font, unlike every other tab. frmCharStat1's form data (MORDOR.EXE RCDATA 8) has them
as separate labels in `elaTabFrame(0)`: `lblStatInfo`, `lblRaceInfo`, `lblGuildInfo` and `lblSpecial`
store no font, so they are MS Sans Serif 8.25 bold (the NonPro font, bold); `elaGroupInfo` ("You are
Alone") is MS Sans Serif 8.25 plain; only `lblStat`'s figures are Courier New 8.25 (the Pro font), set
right. bm13.png agrees. The page now places each at its rectangle (`Mordor.Core/CharStatForm.cs`),
stacked as Form_Resize stacks the loaded copies (FRMCHAR1.BAS:124-183): half a row more before Spells
and Hits, a whole row before Strength, the stats' figures 400 twips wide right after their captions,
and the line above Strength (`elaBorder`) and the "You are Alone" box drawn as the form's Elastics. The
race and title lines are centred, as their Alignment is. Each `lblSpecial` keeps its own place from
Strength's row down whether the ones above show or not, which is how bm13.png has Invisible, See
Invisible and Protected beside Charisma, Dexterity and the row below; the port had stacked the
showing ones from the top. The page grows with a larger Options font as a form page does, across no
further than the window has room for. `selfcheck` reads every rectangle back out of MORDOR.EXE.

**The Guild page is the original's - corrected 2026-09-25.** `mdrcharShowInfo` case `mc059C`
(MAINMOD2.BAS:5335-5359) paints, with `sub09B8`:
- "Guilds" and each guild joined as "Name (level)".
- "Total Skill", the Hall of Records' three indexes: `n Fighting`, `n Magical`, `n Thieving`.
- "Current Guild" and its name.
- At column 11, "Guild Abilities": every ability `fn123F` rates above nought, which is all six since
  it never returns less than 1. Each name is bold, with the rating two rows below in italics as
  "`n` from Guild".

The port listed each guild's level and experience (the experience is not on this page) and a table
of ratings.

A fault kept: the guild is `gv141C.M5493`, which `abilityChance` sets only when some guild beats
nought and never clears. An ability none of your guilds gives anything for is therefore credited to
whichever guild won the call before: the ability above it, or for the first ones the Hall's own calls
(fighting, critical hit, backstabbing, multiple swings, then thieving). A Nomad/Thief shows an ability
neither guild gives as "1 from Thief", because the Thief won the ability before it.

What the global held before the page opened - left by the twenty-odd other `abilityChance` calls
about the game (traps, chests, perception, the combat round) - can never show, contrary to what this
entry said until 2026-10-21. The page's first call is the fighting index's `fn123F(.., 4)`
(HALL.BAS:567), and that case is `mc0080 + ((Log(100 + level) / 1.75 - 2.3) * M55BE^2) / 2`
(MAINMOD2.BAS:1966): `mc0080` is 0.6 and the rest is never negative (`Log(101) / 1.75` is 2.64), so
the first guild joined always beats nought and writes `M5493` before any line reads it. The other
reader, the Char statistics dump (MAINMOD2.BAS:5311-5314), prints the guild only when `M5B95` -
which every call clears first - is above nought, so it always names that call's own winner. The
port therefore starts the page's own sequence at guild 0 and does not carry the global between
the combat's, the chest's and the dungeon's calls; nothing a player can see depends on it.

**The Resist. page is the original's - corrected 2026-09-25.** `mdrcharShowInfo` case `mc0590`
(MAINMOD2.BAS:5209-5219): "Current Resistances" in bold, then the eleven kinds from row 4. Beside
each at column 12 is its percentage, or " -" (with a space) for anything **not above nought**. A
negative resistance therefore reads as a dash, where the port printed the negative figure. The page
is drawn by the same `sub09B8` placement as the Char and Guild pages.

**Who is in the room, and the dungeon readout - corrected 2026-09-25.** `mdrmazLookAround`
(MAZEMOD.BAS:255-346) walks the whole roster and takes everyone on the party leader's floor and in
the leader's area (`M59BB`), plus the leader, counting the dead (other than the leader) apart.

- **Look page.** Bold Name / Guild / Level / Hits headings at columns 1, 9, 15, 19, and a
  plain row for each, the current guild's name and level and the current hits. The port had
  "Name the Race Guild" lines, a "(dead)" suffix and a "Nobody else is here." of its own, and it
  matched by square rather than area. It also left out characters with the Hide option, which the
  original does not check here.
- **Dungeon readout (lblRoom).**
  - The normal form is "x,y,level Facing [ n ]", or "[ n+d ]" when dead are present: the number is
    how many are in the area. The port had read it as the focused member's place in the party and
    put the name after it; dmainw1.png's "26,6,6 West [ 1 ]" has no name.
  - Lost in position gives "(Lost on N) ", and "(Lost) " when the floor is lost too. Lost on the floor
    **alone still prints the level**, because the level is only hidden along with the position; the
    port had shown "?".
  - Lost facing gives "?", and a dead character on screen gives "(You are Dead)".

**When MorgueList is refreshed - modelled 2026-10-21.** The walk reads MorgueList, not the records,
and the list is written at four kinds of point:
- `mdrmazGetChars` (MAZEMOD.BAS:187-232) re-reads it from the roster file: at every level load
  (`fn0A0B`, 438), a chute (1889), the party list's prune (FRMGROUP.BAS:392), a party broken up by
  the break-up check (MULTIMOD.BAS:102), a raise taken in fn16B5 (MAINMOD2.BAS:378) and sub14CC
  (MAINMOD2.BAS:3154), the city's load, the Party Options window's close and a character's
  deletion.
- `sub0956` (MULTIMOD.BAS:710-733) copies each party member's Hits and DeathState, and the
  leader's area and level, into it - only when more than one is loaded. It runs from
  `mdrmazShowMaze` (MAZEMOD.BAS:832) - every step, turn, level or area change and teleport - and
  from the prune (FRMGROUP.BAS:342), the chute and the teleports.
- The walk itself refreshes `gv1A82`'s entry - Hits, level, guild, floor and area, but not its
  DeathState (MAZEMOD.BAS:279-285).
- A body carried, fetched or raised at the morgue has its place written straight in
  (MAINMOD2.BAS:1325, 5988-5991; FRMMORG.BAS:159-171).

This entry used to say that a member killed in a fight is counted among the living until the next
step. It is not: the death sends the party list's UPDATE, whose prune runs sub0956 before it
unloads the body, then mdrmazShowMaze, then mdrmazGetChars (FRMGROUP.BAS:321-393), so the count is
right at once - as the port's prune, which redraws, has it. What does lag is the Look page's
**hits** for party members other than the one the windows show: a combat round, the ailment
clock, a heal, an item, a switch of character and the end of a fight (sub0C53's
mdrmazLookAround, MONSMOD.BAS:791) all leave the copy alone. The port now keeps that copy
(`LookAround.ListedParty`): `Redraw` takes it where the original would have run mdrmazShowMaze,
and the ailment clock, a spell or item that leaves the party where it stands, a switch, a give, a
pool, a leave or a join, the companion and body commands and a pack drop redraw without it.

Left as differences of timing, not of rule:
- mdrmazGetChars re-reads the party from the file, so straight after a prune or a level load the
  rows hold the hits each member was last *saved* with (an area change saves the whole party,
  MAZEMOD.BAS:833-838) until the next sub0956. The port takes the copy from the live records
  there. The frmInteract lists (pick up, raise, fetch, join) read the saves and the party as they
  stand rather than the list.
- The order is no longer one of them (corrected 2026-10-19). The port took everyone else from the
  saves in file order rather than roster order; `Character.LoadAllFrom`, the port's MorgueList, now gives
  the saves in the roster's record order (`Roster.Saved`), as mdrmazGetChars fills MorgueList by
  walking the records (MAZEMOD.BAS:203-224). The Look page, the readout's count, "There is a dead
  body here!" and every frmInteract list (join, pick up, raise, fetch, locate, give) follow it.

An independent review of the change against the source found no other discrepancy.

**"There is a dead body here!" by area, and when - corrected 2026-09-27.** Walking into the room
where a body lay said nothing, because `WhoElseIsHere` matched other characters by their exact
square. `sub09CC` (MAZEMOD.BAS:348-380) counts through `mdrmazLookAround(False, ...)`, which takes
everyone on the leader's floor whose area (M547A) is the leader's M59BB (286-287).
- **Matching.** It now uses the same walk as the readout and the Look page (`WalkHere`): each
  record's stored area, or for a save from before the port kept it the area of its square
  (`Character.AreaOr`). The party's own entries are their live M59BB, which `SyncAreas` (sub0956)
  keeps on the leader's area, so a body in the rock keeps its nought.
- **Counting**, literally as sub09CC does: bodies are the walk's dead (the leader excepted) less one
  per party member carrying a character (354-359); strangers are the walk's living less the party
  size (369-380) - a dead party member is a body and is still subtracted from the living. The port's
  own count had excluded the character on screen rather than the leader, and counted strangers
  apart from the party.
- **When.** Only as the dungeon window opens (FRMDUNM.BAS:282, before sub0835) and when
  `mdrmazShowMaze` finds the area or the level changed (798, 844) - in the port, `SettleArea`, after
  the leave round and before the new room is read (1468). The port had asked on every step and
  never on entry. It had also skipped the lines while lost, which the original does not.
- **Carried bodies.** Just before sub09CC, mdrmazShowMaze writes each carrier's body to the
  carrier's square and area (sub0A7C, 839-841), which is why the carrier subtracts one. The port
  delivered only as the window closed, so a carried body was left behind in its old room and the
  subtraction took a real body off the count. It now delivers there too.
- **Sounds.** 63 with the bodies' line, "YEECH.WAV", and 62 before the strangers' line,
  "HELLO.WAV" (MODULE1.BAS:106-107, 370-371).

**A teleport within one area runs the block too - corrected 2026-10-19.** gv1D6A forces
mdrmazShowMaze's change-of-area block even into the area just left (MAZEMOD.BAS:798). It is set by
the teleporter square (1813), the Teleport spell's mdrcharTeleport once the answer is not empty
(MAINMOD2.BAS:5750-5756), the chest's Teleporter trap (MONSMOD.BAS:3569), the level squares
(MAZEMOD.BAS:113, which always change the level) and the editor (FRMEDITC.BAS:100). There was no pit
in that list: a pit does not set it. Ethereal Portal and Sanctuary do not either. In the same area the block writes
the room back (811), gives an engaged group its leave round (816-819, a hostile group not yet
fighting included), saves everyone, puts down a carried body and runs sub09CC (832-844). The room is
not read again: gv1D66 is moved on only when gv1D6A is clear (789-791), so the test in front of
sub0A91 finds the area unchanged (1467) and the fight goes on with no arrival round. The port's
`SettleArea` had acted only on a changed area or level, so such a teleport cost no free round and
said nothing; it now runs `SameAreaReload`. The flag is set only where an mdrmazShowMaze follows
in the same command and takes it (corrected on review): the Teleport spell sets it past its sky and rock exits, the
chest trap for a lone opener only (MONSMOD.BAS:3591), and a turn (MAINMOD2.BAS:3908) takes any left. The original
sets gv1D6A on those other paths too and carries it to whatever next runs mdrmazShowMaze - a step, a turn or a
repaint of picMaze2 (FRMDUNM.BAS:381-384) - which the port does not follow; it never fires late. Still different:
the block turns timCombat off (MAZEMOD.BAS:817, 828) and with sub0A91 skipped
nothing in mdrmazShowMaze turns it on again (MAZEMOD.BAS:1470-1472), so the original's fight waits for the next
monShowMonsters (MONSMOD.BAS:4420); the port's clock goes on.

**The buffers read as mdcharShowBuffer writes them - corrected 2026-09-25.** MAINMOD2.BAS:4633-4665:
- **Numbering.** Each slot is `LTrim$(Str$(i + 1)) + ". "`, so the rows run 1. to 10. The port
  printed 0. for the tenth, in the character window, in the dungeon list and on the dungeon's buffer
  buttons, whose caption is `l00BA + 1` (FRMDUNM.BAS:248-252, and dmainw1.png shows 10).
- **Cost column.** `lblBufferStat` beside a spell shows its cost to this caster (`fn1485`), or "?"
  when that comes to nought. The port had no such column.
- **Stale slots.** A spell index past the table only *shows* as (Nothing); it is not cleared, and
  firing it clears nothing either.
- **Firing an item.** The item test is the fixed pack slot's `Ref < 1` (6096); the port had
  compared against the compacted inventory's length.
- **Dungeon buttons.** They lost their tooltips and the greying of empty ones. VB3 buttons have
  neither.

**fn1485 returns nought when no guild of yours reaches the spell - corrected 2026-09-25.** The port
put in a point of its own there, and floored the spell's Level and M556D at 1, which the source
does not (2880, 2893). A spell costing nought is cast for nothing, as in the original; one the
character knows is always reached by some guild of theirs, so this shows mainly as the buffer
page's "?".

**Two lines the port added, taken out - corrected 2026-09-25.** An item quest is announced as "You
must return one X." (FRMCTYG.BAS:563); the port said "bring back". The spell-point items (special
types 11 and 12) add their 50 or 200 points and say nothing (MAINMOD2.BAS:6164-6173); "You gain 50
spell points." and its twin were the port's.

**An item's Info is the original's Misc page - corrected 2026-09-25.** `mdrcharShowInfo` case `mc06BA`
(MAINMOD2.BAS:5040-5168) paints the item, reading more of it the better it is identified:
- **Name.** It shows from Mostly up; at All it carries the alignment's tag ("(g)", "(n)", "(e)") and
  " (cursed)".
- **Att/Def.** From Mostly up, with the damage modifier in brackets at All, written as `Str$` writes
  a Single (".5").
- **Class.** It shows with "(Not) Class Restricted" at All.
- **Stats.** Req and Mod rows sit under three-letter stat names.
- **Row 10.** What the special does.
- **Hands and swings.** Given as "One-handed Item, 2 swings".
- **Spell.** "Casts" and its level.
- **Allowed Guilds.** Each with the level it needs (`Str$`, leading space), or "?" below All.
- **Abilities and resistances.** In italics.
- **Identification.** "ID Lev:" and the level, and "This is a Guild-Issued Item".

The port had one line of its own ("att X, def Y   worth N   CURSED   needs ...", and "You can tell
nothing more about it."); that is gone.

Faults kept:
- Row 10's text and the swings suffix share one variable, so an item with hands but no swings reads
  "One-handed ItemModifies Stat(s)".
- Long lists of abilities or guilds run over the rows below.
- The "Unknown" class branch can never be reached.

The pack's Info (in town and in the dungeon) and both of the store's Info buttons now show this
page, as FRMCHAR3.BAS:208 and FRMCTYS.BAS:165/185 send them there; the store's had written to its
own message line. The store's buy Info shows the alignment on sale and nothing for an empty shelf.
A missing item, or a paralysed character in the dungeon (FRMCHAR3.BAS:203), gets no answer, where
the port had said "You cannot make it out at all.". A review agent checked the page line by line.

**Nothing selected, nothing said - corrected 2026-09-25.** The original's pack, spell and companion
buttons leave silently when there is nothing to act on. Its companion buttons are one per
companion (`cmdCompanion_Click (Index)`, FRMCHAR3.BAS:256), and the companion page exits when its
lookup finds nothing (MAINMOD2.BAS:4934-4943). The port's "Pick a companion first.", "Pick a spell
first.", "Choose a spell first.", "Pick something in the pack first." and the companions' "You
cannot make it out at all." are gone. This withdraws the note above that kept "Pick a spell
first." as the port's own. The invented-line guard now lists them, with the other lines removed
today.

**A spell's Info is the original's Misc page - corrected 2026-09-25.** `mdrcharShowInfo` case `mc06BC`
(MAINMOD2.BAS:4813-4933): Spell, Class, Damage or Heal, Guild, Points and Resistance down the
left, "Stats Required" beside them, then the mass line, "Special", "Only Affects", the groups and
monsters, and the spell level it is cast at.

The port's text had two fields wrong:
- **Damage.** The figure after the slash is `M5B41 + 1`, not `M5B28 + 1`.
- **Groups and monsters.** Groups come from `M5B28` and monsters from `M5B1D`. For Firebolt the
  original shows "Damage: 10/1" and "2 groups, 4 monsters"; the port showed 10/3 and 4 groups,
  1 monster.

The guild is `fn1455`, which answers guild 0 when none of yours reaches the spell (the port had left
the line out). The last line's guild is `gv1D08`, set only by a successful `casterSpellLevel`
anywhere, so for an unreached spell it names whatever guild last won, and the level reads 0.

Kept:
- "Resistance:" running into its value.
- The dead "Kills target(s)" branch.
- "Undeads".
- The subscript errors that stop the page part-way. A Class past slot 18, a mass "Resists" and an
  "Only Affects" past the class table all do this; no spell in the data reaches them.

A review agent checked it line by line; the typed-name lookup and the minimised-window case were queued,
and both are built: cmdInfo_Click looks the typed text up with fn1725 (FRMCHAR3.BAS:288-297) -
`Spellbook.FindByName`, with "Spell not found!" and "Spell name is ambiguous!" (MAINMOD2.BAS:6266-6294) -
and mdrcharSetInfo brings the character window back from a minimise or a maximise (4598; see "The
character window is restored where mdrcharSetInfo restores it").

**A companion's Info is the original's Misc page - corrected 2026-09-25.** MAINMOD2.BAS:4934-5039:
- **Heading.** "Comp. #N "Name" (Creature)", or "STORE: Creature" for the pens.
- **Details.** Size, then Type (the class's group) from id 2. Align, A/D and Avg. Hits from id 1.
  The A/D is the companion's, but every place the original makes a companion copies the breed's,
  so it is the breed's.
- **Binding.** Bind or Content Level with its words, for a companion of yours.
- **Stats and Special.** Stats joined with "/" from id 1. At id 0 "Special" beside them in
  italics, overlapping the stats when they are wide, as there.
- **Identification.** "ID Lev:" with "You know All" … "You know Nothing".
- **Picture.** The creature's picture at the top right, under the text, scaled by the window's
  width unless No StretchBlt is set.

Opening it first brings the companion's id level down to what the owner can now tell (`fn1272`,
FRMCHAR3.BAS:752-755), and keeps it. The leaving chance behind the binding words now reads
Charisma with its item modifiers, and rounds `Abs(binding) * 4.5` into an Integer as the source
does.

The port had plain text, with the class name for Type and no picture.

Queued then, and built since:
- The library entry the original's double-click also records (`sub06FE`, FRMCHAR3.BAS:757).
- The Confinement's two Info buttons, which open this page for a pen's creature
  (FRMCITYC.BAS:223-240).

**Chutes are the original's - corrected 2026-09-25.** The port dropped the whole party a level (a
depth of 0 counted as 1), charged nothing, and said "You fall down a chute to level N!". Now:

- **Walking on** (MAZEMOD.BAS:1839-1893).
  - Each member, leader first, falls unless Levitating and the pit's float roll goes their way. A
    member who does not fall, if on screen, is told "There is a chute here.  Select TAKE to use.".
  - A leader who falls with a party behind them is asked "Your party leader has fallen down a
    chute.  Do you wish the party to follow them?" (Follow Leader?); Yes sends every later member
    down regardless.
  - FALL then HIT if anyone fell.
- **TAKE** on the square (MAINMOD2.BAS:3996-4009) sends everyone down with no roll, question or
  sound.
- **Each fall** (sub08F5, 98-139).
  - It adds the record's depth; no record, or depth 0, means no floor change.
  - An unmapped landing square makes that member lost, with the realisation line only for the one
    on screen.
  - Unless Levitating, the member takes `Int(Rnd * (level * 10.5)) * Log(size + 2) + 5` at the new
    level, rounded into an Integer: "You have fallen into a chute for N hits.", or "\"X\" has fallen
    into a chute for N hits." when they are on the leader's floor.
- **Break-up** (sub0908, MULTIMOD.BAS:42-110). Anyone not on the leader's floor "has disappeared
  from view!". They die there if their hits are gone (logged at that floor, DIE.WAV), and are
  saved there and dropped from the party.
- **Afterwards.**
  - TAKE, or a leader followed by a party, runs the landing square's own routine: lost rolls, its
    effects (another chute fires) and its label. A lone walker's is not re-run.
  - Only a party of one dies at nought here (MAZEMOD.BAS:1879-1883). In a party, a member at nought
    or below stays standing, as in the original.

A review agent checked it against the source; its points were all taken up. The only exception is
sub0908's check of the Objects & Spells window's own character, which the port does not have.

**Pits take every member, and the party list buries the fallen - corrected 2026-09-25.**

Pits (MAZEMOD.BAS:1743-1774). The port rolled for the character on screen only. Now every member,
leader first:
- **Floating.** Only a Levitating member can float, and only they draw the float roll.
- **Falling.** A faller takes `Int(Rnd * ((level + 1) / 2 * 15.5)) * Log(size + 2) + 1`, rounded
  into an Integer (the port had cut it), and hits may go below nought.
- **What is said.** The one on screen gets "You have fallen into a pit for N hits." with FALL and
  HIT. Each other member gets a named line on the Messages list and no sound.
- **The step stops.** A lone character who dies there goes no further.

The end of the square routine (1879-1889) is shared with chutes:
- A lone character at nought dies.
- The party list's UPDATE (modUpdateCharacterList, FRMGROUP.BAS:310-352) follows any pit fall and
  any walked chute, even one everybody floated over; a taken chute does not run it.
- That UPDATE drops every member at nought or below, "\"X\" has died!" for those on the leader's
  floor. It marks them dead first if they were not (`mdrcharSetDead`: an ordinary death, counted,
  logged, DIE.WAV). The port had dropped them without a death.
- After each removal it moves the rest to the leader's square (`sub0956`).
- This withdraws the chute note above that party members at nought are left standing.
- The companions of a member dropped this way are no longer released; that is `sub0930`'s part,
  not `mdrcharSetDead`'s.

**The guild hall is frmCityGuild - corrected 2026-09-25.** The port had its own screen: a list of the
character's guilds with experience and fees, "Make active", "Advance a Level", "Other guilds:" with
requirements, and after a level " You gain N hits (now M).", "(No hits: ...)" and "Your X and Y
increase!". None of that is the original's. Stats never rise on a level, and the hits change
silently. FRMCTYG.BAS has:

- **The page.**
  - A list of every guild the race and alignment allow (fn111E), "Name (level)" for those joined,
    with nothing selected.
  - Visit shows the chosen guild: its welcome, its greeting (sub2962), its GuildMaster and the
    stats it asks.
  - Seven buttons: Join (Re-Acquaint for a guild already joined, and the only way to change the
    active guild), Visit, Make Level, Exp. Info, Library, Guild Log, Exit.
- **Join** (84-168). The low-level warning, then the question in a JOIN GUILD box, priced whenever
  you belong to a guild other than the current one. The join cost sums abilities 0-5 and spell
  classes 0-17. Worn cursed gear refuses both join and re-acquaint, and a short purse gets "Not
  enough gold!".
- **Make Level** (179-578).
  - The refusals in boxes, with the Info box behind experience and gold. A quest item is handed
    over before the gold is asked for, so a gold refusal still costs it.
  - The title and spells learned go to the Messages list. The spells are gated over every spell
    record in record order.
  - Then "Congratulations! You are now Level N!", the pack re-read, the GuildMaster and crest
    boxes, the hall records one line each, and the Questmaster ("lets you off the hook" included).
- **Make Level's enabling.** It is re-tested for experience and gold only on opening, on Visit and
  after a level. After Join or a demotion it stays enabled however short you are, and the routine
  itself refuses you, as the original does.
- **Forfeit.** It ends in "forfeit Quest" and "Demotion Complete" boxes.

A review agent went through it twice. The only difference left is that re-acquainting recomputes
nothing, because the port works out the level cap and the A/D interval as it needs them.

**The Bank is bm7.png's form - corrected 2026-09-25.** FRMCITYB.BAS:
- **The form.** "Welcome to the Bank!", Account Status ("You have N gold and M free Bank slots
  available.", counting slots that hold something). A Deposit box and a Withdraw box, each with an
  "All" that fills it and presses Enter. The Party Options, greyed when alone. The
  "Deposit/Withdraw Items" list of names only, unnumbered, empty when empty. Info and Exit.
- **Gold.** Enter moves it silently. Too much, or a deposit of nought or less, beeps (the system
  sound), and the box empties either way. A negative withdrawal is not refused and pays gold in, as
  in the original. The amount is CCur of the text and moves with its fraction (four places): see
  "Gold is VB Currency" (2026-10-21).
- **Items.** They move only by dragging, as there. (The Deposit and Withdraw buttons of 2026-09-28 are a deviation
  the user asked for, through this same code; see that entry.)
  - A pack line dropped on the list goes into the first empty vault slot, holes included. An
    equipped one gets the bank's line, a full vault the "Out of room!" box, and the buffers are
    left alone.
  - A list line dropped on a pack slot comes out there, changing places with what was in it. Not
    Hands, not onto something worn. It may be re-identified, and then the Library learns it too.
- **Dropping onto a list** now lands on the line under the pointer. Before, a drop used whichever
  line was selected, and that also mis-aimed swapping two pack items.

The port's pack list, Store and Take buttons, "Deposit All"/"Withdraw All", "(nothing in the
vault)" and "Your pack is full." are gone. Giving a vault item straight to a party member
(sub14B5's vault branch, `Party.GiveFromVault`) and the guild's check of the vault for an issued
item (FRMCTYG.BAS:703-721, `Guildmasters.AwardCrest`) were queued then and are built since.

**frmInteract's name box, and a batch of the port's own lines - corrected 2026-09-25.** `NamePicker` is
FRMINTER.BAS asking for a character:
- The box offers the characters on the list, the first one chosen.
- A typed name, or the start of exactly one, is taken as it stands.
- "Not Found!" flashes and puts back what was typed; "ambiguous!" flashes and leaves the last name
  that matched.
- An empty list shows its sentence greyed, with OK greyed.

It serves:
- **Ask to Join** (MAINMOD2.BAS:3444-3520), in its order: "Your party is too full!"; the box over
  everyone here not in the party, the dead included; "Enter Password" with a SORRY! "Incorrect
  Password!"; the opposed-alignment line; then "\"X\" is Dead!  Cast a Resurrect spell on the
  character to have them re-join!". A join is silent, with hits and spells restored. The port's
  own join page ("Ask a character to join", its paragraph and its decorated list) and the "Ask
  to join (A)" button are gone, and the party's button reads "Leave".
- **The Seer's LOCATE CHARACTER** over everyone but you. "Name somebody." and "I am unable to
  find anyone called ..." were the port's.

Also:
- A give is silent unless refused, keeping `"X" identified the given item as:`. Gold reads `Give of
  N gold to "X" Completed!`, and nought is silent. "hands the X to Y", "has no gold to give" and
  "You have nobody to exchange with." were the port's.
- The teleport prompts are the original's to the character (MAINMOD2.BAS:1120, 1138). A bad entry
  casts nothing and says nothing, where the port had "That is not a pair of coordinates.".
- The dead screen shows the death-state word, not "Dead on level N" or "In the rock".
- The Objects & Spells "Close (I / Esc)" button is gone; I and Escape still close it.

**Joining in the dungeon, and the party window's buttons - corrected 2026-09-25.** The dungeon's
'a' now matches the city's (MAINMOD2.BAS:3444-3520): "Your party is too full!"; then the name box
over everyone not in the party on this floor and in this area (the area, not the square, and the
dead included); the password; the opposed and dead refusals. The joiner is moved onto your square
before being added, and nothing is restored below ground.

**Ask to Join during a fight - corrected 2026-10-19.** fn1521 has no combat test ahead of case
mc0576 (MAINMOD2.BAS:3380-3444), and the case carries its own fight handling: a dead joiner is
refused with "Cannot raise character in Combat!" while `fn0EEF()` counts monsters (3479-3483), and a
living one is given the Combat Setting by `sub0BDE` (3512). The port had both of those, yet held
the 'a' key (and the toolbar's Ask Character to Join) back until the fight was over; it now asks,
with the fight going on behind the boxes.
- **After the boxes (a port necessity).** The original goes on from each box with no second look; in the port the
  dungeon window may have closed behind them (a wiped party) or the newcomer have joined already, so the window and the
  party are looked at again before the party is touched, and the join is dropped silently if either is gone.
- **A refusal at the last test** (the party's own same-place, alignment and fullness test) unloads the newcomer saved
  as they stand, as the dead-joiner refusals do (mdrcharUnload, MAINMOD.BAS:419-423).
- **Modifiers.** fn1521 case 65 asks nothing of Shift (MAINMOD2.BAS:3444) and fn15DF takes no Ctrl or Alt A
  (4187-4232), so Shift+A, Ctrl+A and Alt+A ask as a plain a does, in the dungeon and in town; the town had let Alt+A
  do nothing.

**Ask to Join asks about the first member - corrected 2026-10-19.** The case opens `If
Chars(m00A6).DeathState = False Then`, with no Else, and m00A6 is the constant 0 (MAINMOD2.BAS:17, 3446): the party's
first member. With the first member dead the key does nothing; a dead member in command under a living first member
can still ask. The dungeon had asked about the one in command, and the city had asked nothing.

**frmParty's UPDATE prunes the dead wherever the original sends it - corrected 2026-10-19.** "UPDATE" on frmParty's
lblCall is modUpdateCharacterList (FRMGROUP.BAS:141-146, 310-389): while more than one is loaded, everyone at nought
hits is set dead if not already (an ordinary death), said "\"X\" has died!" when on the first member's level, put on
the first member's place by sub0956 and unloaded by mdrcharUnload, which saves them as they stand (MAINMOD.BAS:419-423);
the last one left is never unloaded. The original sends it from:
- sub0930 for a death in company (MAINMOD2.BAS:3035-3037), and the round when anyone in it is dead (MULTIMOD.BAS:542-553);
- mdrmazShowMaze on every change of area or level, and on the forced reload within one (MAZEMOD.BAS:819, 827);
- the end of the square routine after a fall (MAZEMOD.BAS:1887), and the chest's trap in a party (MONSMOD.BAS:3652);
- sub0908 once anyone has gone - a teleporter, a chute, the chest's Teleporter trap (MULTIMOD.BAS:88-95, called at
  MAZEMOD.BAS:1878 and MONSMOD.BAS:3590);
- sub154A after a join and after each member the autoload brings (MULTIMOD.BAS:382; MAINMOD2.BAS:3511, MULTIMOD.BAS:266);
- sub1776 after a leave (MULTIMOD.BAS:755).

The port pruned on deaths, the rounds, the falls and the chest's trap. It now prunes at the rest as well: the area
and level changes and the forced reload, each sub0908 (its lines after the chute's and the disappearances), a join and a
leave below ground and in town, the autoload (unheard: sub085B drops every line while gv1B52 is False, MAINMOD2.BAS:3198-3200, as it is through mdrmulLoadParty at 6306),
and sub0930 for a body arriving in town in company (the dead one unloaded if anyone else is standing). In town the death
is mdrcharSetDead's: counted, written to the guild's log at the floor they are on, DIE.WAV. In play most of these find
nobody at nought - the deaths prune as they happen - and they are there for the cases that slip between.

**Shutting the Control Panel runs sub0930 for a dead first member - corrected 2026-10-19.** frmControlPanel's
Form_Unload is `If Chars(m008C).DeathState Then sub0930 m008C`, with m008C the constant 0 (FRMCONTR.BAS:245-247; m008C is the form's Const 0),
so however the panel is shut - OK, Cancel or its close box - a dead first member goes through sub0930 again: the head
(companions let go, the carry put down, sub048C, the save; MAINMOD2.BAS:3026-3034), then in company the party list's
UPDATE, which unloads them while anyone else stands (3035-3037), and alone the dead screen once more, with its boxes -
in town the pending raise's offer and the morgue's box, as on arrival (3039-3125). The port's Control Panel did
nothing on closing; it now does this in town (`ArriveDeadSub0930(Leader)`) and below ground (`Sub0930(Leader)`).

**The dungeon's sub0930 offers a pending raise too - corrected 2026-10-21.** After the dead screen is painted,
sub0930 asks `If DeathState > False And M58E9 > 0 Then If fn16B5(pv) = True Then sub14CC pv: Exit Sub Else M58E9 = 0:
M58F9 = " ": fn04ED` (MAINMOD2.BAS:3058-3066). The city's arrival had it; the dungeon's `Sub0930` stopped at the dead
screen. It now offers the raise (`Sub0930RaiseOffer`, the same RAISE box and roll as on arrival), and on a Yes that
takes runs sub14CC (3134-3155): saved, the clocks running again, lblDead and cmdWait hidden, the room read and the maze
drawn, lblSquareInfo left as it was - or at level 0 the dungeon closed for the city. A No, or a failure without
complications, forgets the raise and saves. In play a body with a raise waiting arrives through the city, so this is
mostly reached by the Control Panel's close above.

**A newcomer takes the first member's lost flags - corrected 2026-10-19.** Once the joiner is
loaded and found not opposed, the case calls `sub0848 True` (MAINMOD2.BAS:3477), which copies
M59F4 0-2 - lost on the floor, in position, in facing - from Chars(0) to the newcomer alone
(MULTIMOD.BAS:454-458). That is before the dead are turned away, so a dead joiner refused by
"Cannot raise character in Combat!", "Drop the character before you raise them!" or "is Dead!" is
unloaded, and saved by mdrcharUnload, with the leader's flags. The port, in the city and in the
dungeon, copied nothing and saved nothing on those refusals.

**The service forms' access keys - corrected 2026-10-19.** Every button caption in a service form's
data in MORDOR.EXE carries its "&" letter, and Alt with that letter presses the button while its
form is the active window - before the form's Form_KeyDown, so a letter no button answers goes on to
fn1521 or fn15DF as before. The port had wired only Exit's Alt+X (and the Guild Log's Done):
- General Store: &BUY, IN&FO, &Sell, &ID, I&nfo, &Combine, Un&curse.
- Confinement: &Bind, &ID, &Sell, I&nfo, IN&FO, &BUY.
- Two letters are shared by live buttons: the Store's C (&Combine, Un&curse) and the Confinement's B
  (&Bind, &BUY). Windows moves the focus between controls whose mnemonic is not unique instead of
  pressing one, and the port's buttons take no focus, so while both are enabled the key presses
  nothing; with one greyed the other is pressed. **Not yet seen in the original** - to be checked
  there.
- Bank: &Info, P&ool && Deposit, &Party Deposit, Party Pool && &Deposit.
- Guild: &Make Level, Exp. &Info, &Visit, &Read Guild Log, Visit the &Library. "&Join" never answers:
  sub2962 recaptions it "Join" or "Re-Acquaint" in Form_Load, before it can be pressed (FRMCTYG.BAS:647,
  787, 807), and where it leaves the form's caption the button is disabled.
- Guild Log: &Clean Guild Log, once Form_Resize shows it.
- Morgue: &Raise Character, &Grab Body, &Hire Rescuers. The companion raise is recaptioned "Raise ...
  for ..." whenever it is shown (FRMMORG.BAS:264), so it has none.
- Seer: &Character, &Monster, &Item.
A greyed button (the Store's and Confinement's Select Alignment wait) does nothing. The keys are
answered from the open service's own page (`CityWindow.ServiceAccessKey`), not as Avalonia access
keys, which reach every window in the frame; while the service is up the town's own Alt pictures
(FRMCTYM.BAS:35-58) wait for the town to be the active window, as before. The city party window always shows Leave,
as bm5.png does. "Pool gold" was the port's button; pooling stays on ALT-p. Exchange stayed there
until the title menu offered it, as FRMMENU does - which it now does (Exchange Items, `TitleWindow.OnExchange`).

**The Seer asks and answers in boxes - corrected 2026-09-25.** FRMSEEK.BAS prompts through fn170D:
"LOCATE MONSTER" / "Name of the Monster?", "LOCATE ITEM" / "Name of the Item?" (a plain line;
empty or cancelled, nothing is said), and the name box for LOCATE CHARACTER. Every answer is a
MsgBox:
- refusals under "Sorry!";
- the monster's under "Locate Monster", the item's under "Locate Item";
- "Not enough gold!" under the service's own title;
- the character's findings, and "Do you mock my abilities?!", under "Seer".

The port had one shared name field and wrote every answer into a label on the page.

**The Seer's name match reads the plural, and Hands - corrected 2026-10-19.** LOCATE MONSTER walks
records 1 on (FRMSEEK.BAS:66) and, for a monster whose name the typing does not begin, tries
`monsterNameFor(Me, 2, l0040)` (74-84). l0040 has Ref and KnownAs set to the record and its IdLevel
left at nought, so that is not a name "the character knows the creature by" but the monster's own
name as the plural is said: "Kobolds" finds the Kobold (an invisible one in
town is never "Unseen", the test wanting a dungeon level). The port matched the plain names only.
LOCATE ITEM walks from record 0 (224), so "Hands" is found, and "Ha" counts it among the matches;
the port had left record 0 out.

**Hiring rescuers is the original's - corrected 2026-09-25.** FRMMORG.BAS:110-178:
- **The button.** "Hire Rescuers" (its caption is in the form inside MORDOR.EXE).
- **The questions.** "FIND CHARACTER" / "Character's Name?" over everyone but you, then
  "Location?" / "Last known coords? (I.E. "15,5,3" for 15,5 Level 3)". Empty, "0" or cancelled
  says nothing.
- **Refusals.** A count other than three is refused in a "Bad coordinates" box, out-of-range
  values in "Sorry!" and a short purse in "HIRE RESCUERS". Each piece is Val'd, so a word counts
  as nought.
- **The outcome.** It goes to the Messages window.

The port had its own paragraph, a list of the lost, a coordinates field, a "Dispatch rescuers"
button and a label for the answers.

**The Seer and the rescuers, put right after review - corrected 2026-09-25.**
- **The Seer's monster location.** It is only roughly right: `sub321C` (FRMSEEK.BAS:413-428)
  moves each coordinate by up to four squares either way before it is told. The map marking the
  port added around it had no source and is gone. His fee is taken as `charPay` takes it, the bank
  making up a short purse.
- **The rescuers' coordinates** are read with the morgue's own comma loop (FRMMORG.BAS:118-135):
  "15,5,3," is three pieces, "1,2," two. Each piece goes through VB's `Val` ("1 5" is 15, "3.7" is
  4). They are compared one-based, as typed, and reported one-based, where the port had been a
  square out.

**The General Store is bm12.png's form - corrected 2026-09-25.** FRMCTYS.BAS:
- **The services.** Uncurse, Combine and Identify/Realign/Sell each take a slot number typed into
  their box and Enter, or an item dropped from the Items window.
  - The value reads "Value", "? Value" or "(g)3 Value".
  - The ID level shows beside ID.
  - G, N and E are live only for a fully identified item that can take that alignment and is not
    already in it.
  - The uncurse fee rounds as a Long.
- **The Buy list.** Nine rows in item order under "Item" and "U G N E", each a price or four
  counts capped at 9. Usable items are bold, the rest plain, not grey.
- **Buying.**
  - Clicking an item stocked in two or more alignments brings up the "Select Alignment" frame
    (U, G, N, E, live only where there is stock), and nothing else can be touched until one is
    chosen.
  - The cost line reads e.g. "3(g) Cost".
  - BUY refuses in "BUY ITEM" boxes ("No free slots!", then "Not enough gold!"). It writes "Slot
    #N now contains your X." to the Messages window.
- **Search for What?** It scrolls to a name's prefix (not while Select Alignment is up) and never
  filters, flashing "Item Not Found!" or "Item is ambiguous!".
- **Combine.** Naming a slot twice no longer pours it twice: the second time it reads as empty and
  is "not compatible class", as in the original.

The port's own pack list, "Add", "Equip / Unequip", Use, Drop, "Buy as" and "(second-hand only)",
the type sort and the filtering search are gone (the grey rows came back at the player's request,
see "Deviation, at the user's request: the Store greys out items" below). The Long rounding of the sell, identify and buy prices was
queued then and is the next entry.

**Store prices round as Longs, unfloored - corrected 2026-09-25.** `modItemCost`, `identifyFee` and
`sellPrice` are Longs, and so is `sellPrice`'s intermediate `l023A` (FRMCTYS.BAS:568-751). Each
result is rounded half to even, not cut, and none is floored at 0 or 1. The port had truncated all
three and added `Max(0/1, ...)` clamps of its own.

**Character creation's guild list and stats buttons - corrected 2026-09-25.** modUpdateInfo
(CHARCREA.BAS:346-379) and bm1.png:
- **The list.** A guild the race cannot join is not listed. Italic marks one the alignment rules
  out, bold one the stats already qualify for, plain the rest. There is no colour, tooltip or
  legend; the port's "Not open to ...", "Needs ..." tips and its "bold = you qualify ..." line are
  gone.
- **Race Stats / Guild Stats.** They open the help at "Races: Statistics" and "Guilds: Statistics"
  (cmdHelp_Click, 105-119, contexts 14 and 15), where the port drew tables of its own.
- **"Reset points"** is gone: bm1.png and the form have no such button.

**The title screen is frmMenu - corrected 2026-09-25.** FRMMENU.BAS, with the captions from the form
resources in MORDOR.EXE:
- **The screen.** No caption, "MORDOR" and "The Depths of Dejenol". The buttons in resource order:
  Character List, Load Character, Delete Character (loaded only), Create a Character, Options...,
  HelpLesson and Exit. With a character loaded, Create reads `Run "Name"` and Load reads "Unload
  Character".
- **Load.** It goes through frmInteract ("Load Character" / "Load which character?"), the password
  box, and "Incorrect Password!" titled SORRY!.
- **Delete.** It asks "Delete Name?" (DELETE CHARACTER). It clears the guild seats the character
  held and refills every empty one (sub0565 True), then removes the record and says "Character has
  been deleted!" (CHARACTER DELETED).
- **Character List.** FRMCHARL's: `Name (Level N Guild) [STATE]`, or `<NONE>`.

The port's own picker rows (race, kills), "No saved characters yet.", the click-again delete, "Load
failed:", the Data Browser button, the sound checkboxes and the tagline are gone. Change Password,
Exchange Items, Hall of Records and About... were queued then and are on the menu since; the
"Unknown error N reading character!" box after the load (FRMMENU.BAS:240) turned out to be
unreachable, so it is not there.

**The dungeon's fight panel is dmainw1.png's - corrected 2026-09-25.**
- **What is gone.** The port's own "Fight (F)", Defend, "Stop (Space)" and Cast buttons, its spell
  list, its order line and its fight log.
- **The panel.** It shows the group list, the two hit captions, the chest, and Wait for Rescue.
- **Orders.** They come from the toolbar and the keys, as in FRMDUNM.BAS:76-123 and fn1521.
  - Fight is F, and engages waiting monsters.
  - D is Defend, and Space is nothing.
  - Spell and C open Objects & Spells, whose Cast makes a known spell the order in a fight. It
    sets it before mdrcharCheckCast speaks, so a refused spell is still the order, with no
    paralysis test (FRMCHAR3.BAS:174-184).
- **The order mark.** monShowCursor's "+", "[", ">" or "•" sits beside the targeted group
  (MONSMOD.BAS:4198-4229). A target whose group is gone is moved to the first group standing and
  written back.
- **The Messages window.** Each sub085B line of a fight goes there as it is said, and the
  share-out's lines at the end. The hit captions do not: they are lblCharacterHit and
  lblMonsterHit, not messages. The port had written the whole fight log into Messages at the end,
  captions and all.

**Experience on the blow, the city gate, and two roundings - corrected 2026-09-25.**
- **Experience.** It is paid as each blow lands, through sub0CDF (MONSMOD.BAS:468,
  495, the companions' 943-1287, spells at MAINMOD2.BAS:1483-1485) and so sub0F11 (MAINMOD2.BAS:279-310), not held until the fight is
  won. A party that flees keeps what it earned. "... now pinned." and "... now has enough
  experience to make a level." are judged, as sub0F11 judges them, by the total before and after
  each award. The second tests fn12A1 alone, with no guild-ceiling test, and both are Messages
  lines.
- **The city gate** ends every spell effect (FRMCTYM.BAS:111), for every member.
- **A fire trap's damage** goes back into Hits rounded, not cut.
- **The disease cure roll** subtracts nothing for poison on the tick that cured the poison, because
  Ailments(0) is zero by then (FRMDUNM.BAS:538-547).

**Water, quicksand, a chest's receiver, the group list, a swap and an empty shelf - corrected 2026-09-25.**
- **Water and quicksand** follow FRMDUNM.BAS:500-531, member by member.
  - The square's label says "You are Drowning!" or "You are Sinking!" whoever it is happening to.
  - The water's sound plays only for the one on screen.
  - A quicksand loss is a Messages line: "QUICKSAND: X Destroyed!", with the quoted name in front
    for anyone else.
  - A death there says nothing of its own. The port's "drowns." line was its own.
- **A chest's item** goes to `Chars(CInt(Rnd * UBound(Chars)))`, rounded half to even as CInt does.
  So the first and last members are half as likely as the others; that is the original's.
- **The fight's group list** no longer lists the companions under "with you:"; the original's
  list holds the monster groups only.
- **Swapping with an empty slot** is refused silently, as FRMEXCHA's is. The port's "Nothing
  there." was its own.
- **An empty shelf is priced at 0** (FRMCTYS.BAS:587-615); the port had floored the count to find a
  price for it.

**Pickup identification, a slay-class spell on a companion, a level-0 breather and a floor-0 price - corrected 2026-09-25.**
- **A picked-up item's identification** is fn12F0's (MONSMOD.BAS:3828, MAINMOD2.BAS:2480-2499).
  - Skill and difficulty are both rounded half to even into Integers before they are compared.
  - The difficulty takes the item's modifiers as they are, with no floor at 0.
  - A guild level of 0 is no special case: its skill of 0 simply loses.
- **A monster's class-slaying spell at a companion** (effects 75-99) is tested against the monster
  its owner is fighting, `Foes(CombatOrders(owner).M547A)` (MONSMOD.BAS:2561-2564). The port had
  tested the first living group.
- **A monster of guild level 0 that breathes** takes `Log(0)` at MONSMOD.BAS:2708.
  - The error handler (MONSMOD.BAS:3414-3417) resumes at End Sub, so that group does nothing
    more that turn: no breath, no melee and no spells.
  - The chance roll has already been drawn by then.
  - The port had guarded the logarithm and let such a monster breathe.
- **A floor-0 item's store price** is its list price, as FRMCTYS.BAS:594-597 has it. That test
  looks at the floor alone; the port had also asked for a positive price, a type other than 34,
  and a nonzero id.

**The four-second tick runs in the original's order - corrected 2026-09-25.**
- **Before anyone's turn,** the ambient noise is rolled (FRMDUNM.BAS:445-449).
- **Each member, in turn** (FRMDUNM.BAS:451-584), is taken through these steps:
  1. identifying the groups in front of them;
  2. water and quicksand;
  3. poison, then disease;
  4. paralysis wearing off;
  5. their companions (sub0C00);
  6. old age;
  7. the death check.
- **What the port had done instead.** It rolled old age first, and a member who died of it skipped
  everything else. It then ran water and identification for the whole party after everyone's
  ailments.
- **Liveness is asked once, at the top of a member's turn, and the loop goes by index.** A death
  takes a party member out at once: sub0930 sets lblCall to "UPDATE" (MAINMOD2.BAS:3035), and its
  Change handler (FRMGROUP.BAS:141-146) updates the party list there and then.
  - So after a drowning, poison through the death check fall on whoever has moved into that place.
  - If the drowned member was last, the clamp at FRMDUNM.BAS:532 gives the one before them a second
    round of poison.
  - A character alone keeps the body, which takes the rest of the tick. mdrcharSetDead is then a
    no-op, but if its age comes up it is still announced as having "died of natural causes".
  - Faults of the original, kept.
- **"You have died of natural causes."** is for the one on screen (`l0144 = Me`), not for whoever
  is alone.
- **A cure of poison or disease on this clock** plays BETTER.WAV (sound 40), as the original does.
  The port had played nothing.
- **The ambient noise** needs the leader alive (FRMDUNM.BAS:445-446), so a body waiting for rescue
  no longer hears it. The loop no longer stops when nobody is able: a stoned party still ticks,
  and the sounds and cries after the loop still come.
- **After everyone** (FRMDUNM.BAS:586-591), the leader's square is heard:
  - FOG.WAV a quarter of the time in fog;
  - WATER.WAV or WATER1.WAV on water or quicksand.

  The port had neither.

**The chest's pickup line, a failed key, and the access keys - corrected 2026-09-25.**
- **An item from a chest** is announced as MONSMOD.BAS:3832-3838 has it: "CHEST: Slot #n, item".
  - The container is one of Container, Box or Chest (MODULE1.BAS:425-427), in capitals.
  - The slot is the one it landed in, counted as the pack counts.
  - The item is named as the taker can read it, with its alignment and charges.
  - In a party the taker's quoted name comes first, whoever took it.
  - The port had printed the number of things carried and the item's barest name, and quoted the
    name only for someone other than the one on screen.
- **An error while handling a key** is written to the error file and not shown. That is what sub04FC
  does unless the game was started with /DEBUG (MAINMOD2.BAS:1689-1700, 4169-4171). The port's
  "Something went wrong handling that key..." line is gone.
- **Access keys, from bm0.png and bm3.png:**
  - Control Panel: &Hide, &Preload Char., &No Perception Msgs., &Auto Change Lev, &Reset, Adjust to &Display, &Show,
    &OK and &Cancel.
  - Options: &Default, &OK and &Cancel.
  - The dungeon's &Wait for Rescue / &Visit the Morgue (MAINMOD2.BAS:3045-3049).
- **Removed.** The morgue buttons' red and green backgrounds and the "Not built yet." tooltips on
  Options had no source; VB3 buttons have no tooltips.

**Confinement & Holding is frmCityCStore - rebuilt 2026-09-25.**

**The page.** It is laid out as bm9.png shows it (FRMCITYC.BAS):
- **Bind Companions:** a Comp. box, Cost and Bind.
- **Identify, Realign & Sell:** a Comp. box, Value, G/N/E, Info, Sell, ID and ID Cost.
- **Buy Companions:** nine rows of " Name" beside the G N E counts (or "PRICE" for a creature from
  floor 0), a scrollbar, Companion, Cost, BUY, INFO and Search for What?.

**Choosing a companion.** You type a slot number into a Comp. box and press Enter (fn33DB). The box is
then rewritten as `1. (Kobold)` or `1. "Fido"`.

**What had no source, and is gone:**
- the port's own "Your companions" and "In the pens" lists and their row formats;
- "(no companions)" and the pens-empty line;
- the "realign:" label and a message line of its own.

**Prices.**
- BaseValue follows the source (FRMCITYC.BAS:548-603): only positive resistances count, a
  creature of 1 hit is worth 1, and the port's own surcharge for spell bit 11 is gone.
- Every price rounds half to even into its Long.
- The bind cost's factor is a+4, or 12-a past twelve (FRMCITYC.BAS:606-618). The port had used
  the next binding level.

**Identifying** files the creature in the library as sub06FE does, raising what is known of it.

**Faults of the original, kept:**
- ~~**The G/N/E alignment picker.** It appears on the first row click and never goes away.~~ A
  misreading, corrected 2026-09-29: see "The Confinement's Select Alignment frame comes and goes",
  below. The kept fault is narrower: after a character switch with the list's scroll bar greyed
  (lblCall_Change, FRMCITYC.BAS:468-470) the frame comes up with no wait running, nothing takes it
  down, every row click beeps, and its buttons change what BUY charges without changing the Cost
  shown (cmdAlign_Click, 50-52), until the window is shut.
- **A stale row can still be bought from.** A row whose stock has run out still enables BUY. The
  price then takes Log(0), whose error trap makes it free and writes the error to the error file,
  and the pen goes to -1 (FRMCITYC.BAS:140-142, 620-641).
- **Handlers read the box as it stands.** Sell, ID, Info and Realign take the Comp. box as it is
  when clicked. So Sell has no dead check of its own, and a slot never entered can be sold.
- **Bind's second check.** Bind re-checks the binding cap but not free will.
- **"NO COMPANIONS!"** is coded, but can never show (FRMCITYC.BAS:506).
- **The list never shrinks while the window is open.** A creature that sells out leaves the last
  row doubled.
- **The restock's cap test reads one pen variable, `l0288`** (MAINMOD.BAS:1372-1373, 1438-1447).
  It is Good until a take-away pass moves it, and the pen it tests is seldom the one it fills.
  So pens run past nine.
- **A party switch with nine creatures or fewer leaves the list busy.** lblCall_Change sets the
  busy flag when the scrollbar is disabled (FRMCITYC.BAS:468-470), and every row click then beeps.

**Not ported then, built since:**
- **Selling leaves a gap**, as in the original: companions are four fixed slots now
  (`CompanionSlots`), so the ones after it keep their numbers.
- **Dragging a companion onto a Comp. box** works since 2026-10-18 (FRMCITYC.BAS:744-796): the
  slot's number and Enter, as the original's DragDrop does.

**Paralysis belongs to the character - corrected 2026-09-25.**

It is `Ailments(2)`, a level saved with the character. The port had kept it in the fight, and
lost it the moment the fight ended or the party fled.

**Setting it.** sub0D1D's guards are copied (MONSMOD.BAS:3884-3906): a character already paralysed
is not paralysed again, and one who is not is not cured. The level is the monster's guild level,
with no minimum.

**While paralysed**, Att and Def lose their stats part, as sub0D1D takes it off.

**It lasts until one of these frees them:**
- the four-second tick, which always frees them when no monster is on the square
  (FRMDUNM.BAS:552-562);
- their companions leaving no foes (MONSMOD.BAS:1029-1033);
- Cure Paralysis, which frees the caster (MAINMOD2.BAS:917-928);
- the city gate or death, which clear every ailment.

**What a paralysed character cannot do.** These are refused silently, as in the original: moving
and turning, stairs up and the exit, picking up, dropping or giving a body, the fight key, opening a
chest, and the character window's buttons.

**Faults of the original, kept:**
- **Only the way up is barred.** A paralysed character can still go down stairs or take a chute,
  because MAINMOD2.BAS:3953 wraps the up case alone.
- **A level-0 monster paralyses at level 0,** which holds nobody.
- **A mishap stays paralysed.** A character killed by a companion's mishap keeps the paralysis,
  because that routine never reaches sub0930.

**Also kept (since 2026-09-26, through a stored Character.ADShift, as the original stores Att and Def):**
- **Permanent losses of the stats part.** The city gate zeroes Ailments(2) before sub048C can give
  the stats part back. A level-0 paralysis strips it and nothing ever restores it.
- **Drift.** A cure gives back the stats part as it stands then, so stats or gear changed while
  paralysed shift Att and Def for good.

**Picking up a body is frmInteract's PICKUP - corrected 2026-09-25.**
- **'p'** (MAINMOD2.BAS:3654-3683) asks "Character's Name?" in a box titled "Pickup Character".
  - The list holds the dead on your floor and in your area (FRMINTER.BAS:339-387), leaving out
    yourself and anyone a party member already carries.
  - An empty list is the greyed "No other characters in the area!".
  - A hidden character is listed; picking one gets `"X" is Hidden!` and nothing else happens.
  - Otherwise whatever you carried is put down first, with its own line, and the new body is
    taken up.
  - The port had listed everyone on the same square, left hidden characters out, offered your own
    dead companions as "(your companion)" rows, and labelled every row "Name the Race Guild".
- **The morgue's Grab Body** (FRMMORG.BAS:58-64) asks the same question. It then only takes the body
  up: no Hide test, nothing put down, and nothing said.

**Raising someone else is an offer, not a roll; the morgue is frmMorgue - corrected 2026-09-25.**

**The pending raise.** Raising another character marks the body and rolls nothing
(sub0A7C, MAINMOD2.BAS:5974-5998). It moves the body to the raiser, and records three things:
- the chance;
- the raiser's name;
- the kind of raise: 23 at the morgue counter, or the spell's effect.

**The offer.** When that character is next played, fn16B5 (MAINMOD2.BAS:314-380) asks: `"<raiser>" has
initiated a raise on your character. ... Chance of success is N%. ... Do you accept?` The roll, and
then its outcome, happen only on a Yes:
- success, with the age added and the hits restored;
- failure;
- complications.

**The Raise spells.** They now pick from frmInteract's SPELLRAISE list: bodies on this floor and in
this area, not carried and not already pending, plus the caster's own dead companions. A spell
cast on a body says only `"Spell" thrown on "Name".` The port had rolled on the spot.

**The morgue page** is bm10.png's:
- "Welcome to the Morgue!";
- Raise Character and Hire Rescuers, then Grab Body;
- Raise Companion;
- Exit.

**Raise Character** (FRMMORG.BAS:66-88):
- It asks the PICKUP question.
- It refuses a rocked body with the original's "resurrect" line, and a pending one with "Raise
  already cast on Character!".
- It charges 2912.32 x MaxHits / 1.5, rounded half to even.
- It marks the body with 35 x (Con / 4.5) + 10, clamped 10 to 98 and rounded as an Integer.

**Rounding.** The raise chances now round half to even. The port had truncated them.

**A dead character's screen** is no longer part of the morgue page. frmMorgue never sees the dead
(MAINMOD2.BAS:3170-3182). It follows sub0930's order: any pending offer first, then the morgue's
own offer at level 0. The rock body gets its own line and no wait button.

**Faults of the original, kept:**
- **A rocked body is still offered its raise,** at 0%, so it always ends in complications.
- **A taken raise keeps its kind.** When a raise takes, its kind field is not cleared.
- **The morgue's Constitution loss** is spelled "Constution" and has no floor.
- **A raise spell refused on stone or rock still costs its spell points.** The points come off
  after the effect's Select (MAINMOD2.BAS:1355-1356). The port had refunded them.
- **A raise spell on another character is not refused when one is already pending;** it only
  leaves pending bodies off the list.
- **Raise Companion:**
  - it is never hidden by a rescan;
  - its "Not enough gold!" box is titled "RAISE COMPANION" and has no icon;
  - whatever is carried is put down without a word.

**Companions live in four slots, and the fallen stay in them - corrected 2026-09-26.**

**The slots.** A character has four companion slots, and an empty one is `Ref = -1`.
- A new companion takes the first empty slot (fn1163).
- Selling or releasing one leaves a hole; the others keep their numbers ("Companion #n",
  the Comp. boxes, the carry).
- The port had kept a list that closed up.

**A fallen companion** stays in its slot at 0 hits.
- It does not fight, drift or leave.
- It is shown like any other, with `0\Max`.
- Acid can still reach it.

**Who carries a fallen companion.** mdrcharGetCarry (MAINMOD2.BAS:2027-2084) runs at every death and
when a member joins or loads.
- The heaviest fallen companion is carried: the most MaxHits, the earlier slot on a tie. Its lines
  are "dropped X to carry Y", "You are now carrying X." and "X was too big to carry.".
- The rest are let go.

**The morgue's Raise Companion** (sub2BDB) now appears when a slot holds a fallen companion.

**What lets a companion go.** Dropping a carried companion, or picking a body up over one, lets it
go. Death lets go of every companion and anything carried (sub0930).

**Faults of the original, kept:**
- **No choice while carrying nothing.** If a later fallen companion outweighs an earlier one,
  the carry routine errors on fn128B(-1) and nothing is carried or let go.
- **"dropped X to carry Y"** names the slot being carried, and comes before the size test, so
  "too big" can follow it.
- **A carried morgue body lets go of every fallen companion.**
- **The carry can point at a hole.** Stoning or selling the carried companion leaves the carry
  there, still showing the old name.
- **Fear and Grab Body** leave a fallen companion in its slot, uncarried.
- **Breath and acid at exactly 0.** They test below 0, so a companion brought to exactly 0 is
  not carried until the next sweep.
- **Acid** never hits slot 0 and wastes draws on a fifth slot that does not exist.
- **An empty slot keeps its old name.** Only the companion panel blanks it, and only for the
  character on screen. So a charmed or joining companion can inherit it. Since 2026-10-19 that
  record (name, hits and the rest) also survives a save and a reload, as the character file keeps
  all four records (MAIN.TXT:339); the port had written an empty slot as null and read it back as
  the creator's blank (CHARCREA.BAS:257). It is written marked `Vacant`, and a slot still holding
  the blank is written as null, so older saves read as before.
- **Acid repaints nothing.** A companion killed by acid is not followed by a repaint of the panel
  (MONSMOD.BAS:2800-2806), so the names of the slots its sweep emptied survive until the next one.
- **A companion's spit can land on an empty slot.**
  - It is announced, and the damage is worked from Chars(0).
  - If the leftover record falls below 0 while sound effects would play, the death sound reads
    Monsters(-1). The error ends that owner's companions' turn.

**Not kept:** the port keeps one "character in command" where the original keeps Me and gv1A82
apart, so the "too big" line's owner test cannot tell them apart.

**The city's lines go to the Messages list - corrected 2026-09-26.** Every city message the port wrote into
its one-line MsgLine is a sub085B line in the original (MAINMOD2.BAS:3195-3208): an entry in the Messages
list, which keeps the last twenty. They now go there, one entry per sub085B:
- the rest and carry lines on arrival are separate entries, not one line joined with spaces;
- the next-level line and the quest line are separate entries;
- every STORE combine line is logged, not just the last.

The four places where the port blanked the line afterwards are gone; the original says nothing there. The
exception is a give to somebody whose pack is full, where `"X" has no free slots!` is a box titled
SORRY! (MAINMOD2.BAS:2953). MsgLine itself, an invisible control with no counterpart on any city form, is
gone.

**The Party Information window is frmParty - corrected 2026-09-26.** It is bm5.png's in the city and the
dungeon alike: Name / Hits / Spells / Status / Option, then Switch To and Options on the left and Leave on the
right (FRMGROUP.BAS:32-46).
- **Colours.** The selected member's Name and Hits are green and the Name alone is bold
  (MULTIMOD.BAS:640-642). Status is blue when OK and red otherwise (FRMGROUP.BAS:446-451), and Spells and
  Option are blue. The port's ▶ marker and its own colours for the dead and the ailing are gone.
- **Buttons.** The city gains Switch To and Options; Options opens the same Party Options window as in the
  dungeon.
- **Exchange.** The city's Exchange button had no source: Exchange is a Main Menu item (FRMMENU.BAS:68-86)
  and was queued there; it is on the main menu now (`TitleWindow.OnExchange`, FRMMENU.BAS:68-87).
- **"OK".** bm5.png shows "Ok" where the source writes "OK" (FRMGROUP.BAS:447); the port follows the source.

**Party Information is drawn to frmParty's form data - corrected 2026-09-27.** In a saved layout about
360 pixels wide, a fifteen-letter name ran into Hits, and with three or four members the three buttons were cut off
at the bottom. The port had laid the window out in its own sizes: a star Name column after four fixed ones, theme
buttons, and contents drawn at 340x110 and only ever scaled down. frmParty's form data in MORDOR.EXE (RCDATA 44)
has the layout, in twips:
- **The client** is 4365x1485 (291x99 pixels). The header labels are at the top. The member rows are in a second
  Elastic at 45,210, and each row is 195 twips below the one before (FRMGROUP.BAS:375-379).
- **The row labels** are lblName 30,45 1275x195 (left-aligned), lblHits 1095 660 wide (right), lblSpells 1830 615
  wide (centred), lblStatus 2475 600 wide (right) and lblOption 3255 900 wide (right). All are opaque MS Sans Serif
  8.25.
- **The buttons**, cmdCommand(0..2), are 945x300 at top 1125: Switch To at 30, Options at 1020, Leave at 3345.
- **Long names are cut.** lblName is wider than the gap to lblHits, and each loaded lblHits is brought in front of
  it (FRMGROUP.BAS:371), so a long name is cut where Hits begins. The port cuts every row there. Row 0's lblName and
  lblHits come from the form itself, in the form's own order, and that order is not decoded here.
- **The whole form zooms.** Form_Resize ties the height to the width (FRMGROUP.BAS:117-122), sizes the Elastic to
  the client (sub07B3) and scales every font by the width over the default (sub07C5, MAINMOD.BAS:1925-1975).

The port now draws these rectangles at 291x99 in Microsoft Sans Serif 11, as the city and dungeon forms are drawn.
It scales the drawing to the window both ways (ShrinkToFit.Grow), with no body margin, so all four rows and the
buttons show at the default size and at any saved size. Since 2026-09-27 it zooms as Form_Resize does: by the width
(sub07C5's font ratio), with each control's top moved in proportion to the height, as the Elastic moves it
(ShrinkToFit.ByWidth, ElasticCanvas). It had scaled by the smaller of the two ratios, which left a blank band beside
or under the drawing and put Leave out past the drawing's right edge; a window's height already follows its width. The port's text is 11 pixels, the form data's 8.25 points.
After a resize sub07C5 sets MS Sans Serif controls to NonProSize, 8 points, times the width ratio: 11 pixels too, and
since 2026-09-28 the Options window's NonPro font and size ("The Options fonts are applied", below). A mod
column (docs/MODDING.md) still widens the drawing through ShrinkToFit.ExtraWidth; Leave stays 4 pixels from the
right edge. `tools/check.sh` reads these rectangles out of MORDOR.EXE and holds the port to them. The dungeon's
Party Information is the same form, and it now has the same layout. It had the port's own column widths
(`*,56,52,66,60`), and a fifteen-letter name ran into its Hits column too.

**The town's service windows are drawn to their forms' data - corrected 2026-09-28.** The Guilds window was cut off
at the bottom: Make Level, Join and the rest were out of view. Its page was taller than the window, with an outer
scroll and an inner scrolling guild list whose rows were the theme's, about 48 pixels apart. The window had been
sized to the page as it opened, and the page was the port's own layout, far roomier than the form. The Bank's
status line wrapped over three lines, and every service had two exits. Now all six services are drawn to their form
data in MORDOR.EXE: the Guilds, the Bank, the Morgue, the Seer, Confinement & Holding and the General Store.

| Form | RCDATA | Client (twips) | Client (pixels) | elaElastic(0) |
|---|---|---|---|---|
| frmCityGuild (FRMCTYG) | 20 | 5220x3420 | 348x228 | -15,-15 |
| frmCityBank (FRMCITYB) | 18 | 6195x3945 | 413x263 | 0,-15 |
| frmCityMorgue (FRMMORG) | 28 | 3480x1905 | 232x127 | 0,-15 |
| frmCitySeer (FRMSEEK) | 52 | 5025x1755 | 335x117 | -15,-15 |
| frmCityCStore (FRMCITYC) | 60 | 6510x4080 | 434x272 | -30,-15 |
| frmCityStore (FRMCTYS) | 16 | 7170x4380 | 478x292 | -15,-15 |

- **The client.** Each client is 120 twips narrower and 405 shorter than the window table's default for the form
  (MODULE1.BAS:606-648). That is the form's outer size: its Fixed Double border and its caption.
- **Every control at its rectangle.** Controls sit at the form's own rectangle, in twips over 15. Each sits in its
  container: elaElastic(0), which covers the client and a pixel beyond, or the smaller Elastics inside it.
  `Mordor.Core/ServiceForms.cs` holds every rectangle. `tools/check.sh` reads each one back out of MORDOR.EXE, and
  each form's client too. It also checks that each page places every rectangle.
  - **The Guild.** lstGuilds is 3360,630 1755x1980. The buttons are Make Level 180,2415, Exp. Info 1725,2415,
    Join 180,2730 and Read Guild Log 1725,2730, all 1450-1455x300. Visit is 3375,2715 1755x285. Visit the Library
    is 180,3060 3000x300, and EXIT 4050,3045 1080x315. The stats are lblStatName and lblStat in elaElastic(1),
    165,1125 3000x585. Form_Load loads each copy at the one before's Left + Width (FRMCTYG.BAS:631-640).
  - **The Bank.** lblStatus is one line, 5850 twips wide, in elaElastic(1). lstItems and Info are in
    elaElastic(3), and the three Party Options buttons in elaElastic(2).
  - **Confinement.** Its nine creature rows are elaComp and elaCompInfo. Each is loaded 225 twips below the one
    above (FRMCITYC.BAS:498-503). vsbComps is 3030,255 270x2100. elaAlignments is captioned "Select
    Alignment" (Label11) in the form data; the port had left it uncaptioned as unknown.
  - **The Store.** Its Buy list is still one grid, over the form's own list. The grid runs from Label9's headers
    (top 75) through the nine elaItem rows, 210 twips (14 pixels) each from 300, to vsbItems' right edge. That is
    105,75 3855x2115 in elaElastic(4), and the grid's own scroll bar stands in for vsbItems. The counts are
    elaItemInfo's Courier New 8.25 bold, 11 pixels; they had been 14. The Item and Cost fields are
    elaSelectedItem and elaSelectedItemCost, Arial 8.25. The Select Alignment frame (elaAlignments, 930,1005
    1905x675, four cmdAlign) is drawn over the list when shown, as the form has it. It no longer keeps a band of
    the page empty while hidden.
  - **The Store's mod extras** (docs/MODDING.md) still work. A mod's columns widen the list, the Buy Items panel,
    the page and the window by the same amount, and EXIT keeps its place from the right. The "Hide unusable" box
    sits on the Buy Items heading's line, right of the caption. A sortable header still takes clicks. With mods
    off, none of this is there, and the Store is its form.
- **Fonts.** A control with no font of its own has the form's, MS Sans Serif 8.25 bold, drawn at 11 pixels. That
  covers EXIT, Visit the Library, the captions, the Seer's four buttons and the Bank's list. The ones with a font
  of their own are 8.25 plain: the Guild's other buttons, the Morgue's five, and the Bank's All, Info and Party
  Options. Each welcome is 12 points bold, 16 pixels. The Guild's stats and the Confinement's counts are Courier
  New. Since 2026-09-28 every one of them goes through the Options window's fonts (see "The Options fonts are
  applied", below): with the defaults the Courier New ones are 9 points, 12 pixels, as sub07C5 makes them.
- **List rows.** They had been VB's 13 pixels for MS Sans Serif 8.25. Since 2026-09-28 they are the font's line
  and three pixels, 16 with the default fonts (a deviation, "The Options fonts are applied", below). The theme's
  roomy item padding is gone (`FormPage.TightRows`).
- **Colours.** The form's blue, maroon and navy are drawn as the dark theme's cornflower, indian red and a light
  navy.
- **The window is the client.** Its frame goes round the client, with no margin, heading strip or scroll bar. A
  redraw in place builds the same size again: the Guild's lblCall "UPDATE" (RedrawGuildInPlace), the Bank's
  refresh, the Morgue's, the Confinement's, and the Store's refilter. The window still fits it afterwards.
- **The port's EXIT strip is gone from all six.** It was the port's own: a heading strip over every service page,
  with the caption and an E_XIT button. No original service form has one. The caption is already on the window's
  title bar, and each form has its own Exit: cmdExit, or on the Seer cmdCommand(1) (FRMSEEK.BAS:362-363). The
  port's Seer had had no Exit but the strip's, and the port's Store had used the strip's as its form's EXIT, which
  the Select Alignment wait greys. That wait now greys the form's own EXIT. Each form's Exit keeps the strip's Alt+X
  (E_XIT / E_xit). The rest of the form data's access keys are wired since 2026-10-19 (see "The service forms' access keys" below),
  scoped to the active service window, which an Avalonia access key is not.
- **The Seer's frame is "Seer Options".** The form data says so. bm11.png's "Locate Options" is from another
  build.
- **The Guild's list starts from its top after a redraw.** sub2974 clears lstGuilds and fills it again. The port
  had kept the list's scroll across the redraw.

DEVIATION: Form_Resize zooms each form to the size kept for it on the character (sub07B3, sub07C5). The port
draws the forms at their own size, and places them where the character keeps them. Those saved sizes were
written by the port's earlier, larger pages, and none of them is the form's. The size is written back as it was
read. The Library, the Guild Log and the Hall of Records are now drawn to their forms too (next entry). The dead
screen keeps the port's heading strip, and hides the strip's EXIT, as before.

**The Library, the Guild Log and the Hall of Records are drawn to their forms' data, and the Library's Search is
back - corrected 2026-09-28.** The three were the port's own roomy layouts: the Library a 300-pixel list beside a
scrolling page, the Guild Log a 420-pixel list, both under the port's EXIT strip, and the Hall a dialog 8 pixels
narrower and 7 shorter than its form, with its controls a pixel off. The Library had no Search box at all. Now each
is its form in MORDOR.EXE, read the same way as the six services (`Mordor.Core/ServiceForms.cs`, held to the EXE
by `tools/check.sh`):

| Form | RCDATA | Client (twips) | Client (pixels) | Elastic |
|---|---|---|---|---|
| frmLibrary (FRMLIBRA) | 56 | 7710x4410 | 514x294 | elaElastic(0) -15,-15 |
| frmGuildLog (FRMGLDL) | 48 | 7755x2490 | 517x166 | elaElastic (no array) -15,-15 |
| frmHall (FRMHALL) | 58 | 7665x5055 | 511x337 | elaElastic(0) -15,-15 |

None of the three has an entry in the window table, so there is no default size to hold them to. The Library and
the Guild Log open over the Guild, modally (FRMCTYG.BAS:170-177), in the service window, centred, at the form's
client in its frame and not sizable; closing either goes back to the Guild. The Hall is the main menu's own
modal dialog.

- **frmLibrary.** "Browse" (Label1, navy) 90,90; elaElastic(1) 105,345 1215x645 holds optOption(1) "Monsters"
  (Value True) 45,45 and optOption(0) "Items" 45,330, each 1100x270; picMonster 1560,105 900x900 - the sheet's
  60-pixel cell; lstList 90,1095 2370x2175; txtInfo 2565,90 5070x3795; "Search For:" (lblLabel) 90,3345;
  txtSearch 105,3600 2385x285; lblEntries 75,4050 6015x210, navy and set right; Exit (cmdCommand(0)) 6165,3990
  1485x315. No control has a font of its own, so all are the form's MS Sans Serif 8.25 bold.
  - **lstList is a Sorted ListBox.** Its record has a property 15 = FF before the FF that ends a control's
    create-time properties, where txtInfo has its MultiLine (17 = FF) and ScrollBars (18 = 02). A ListBox's
    create-time properties are Sorted (a Boolean), MultiSelect (0 to 2) and Columns; FF can only be Sorted = True.
    So the names sub327E adds in the data's order are shown alphabetically, without regard to case. The port had
    shown them in the data's order, and had said the original did.
    The order is a Sorted ListBox's: Windows compares as lstrcmpi does, a "word sort" in which a hyphen and an
    apostrophe count for nothing at first, so Tarantula, Tazen, T'Butu, Tengu, ..., Tiger Shark, T'Kovant, and
    Iron Bear, I'ssus, Jestor. The port had sorted with .NET's culture compare, which (ICU) put every T' creature
    before Tarantula and I'ssus before Ice Demon. On Windows the port now calls lstrcmpiW itself; elsewhere a
    managed stand-in drops hyphens and apostrophes and compares without regard to case, and check.sh holds it to
    lstrcmpiW over every MDATA5 and MDATA3 name. Names equal but for case keep the order they were added in.
  - **The Search box** (txtSearch_KeyDown, FRMLIBRA.BAS:130-156). Enter counts every entry of the list now shown
    whose start is the typed text, compared in upper case, and keeps the last. One match is selected - its page
    and picture come up, as lstList_Click gives them - and the box is cleared. None puts up "No match was found."
    and more than one "ambiguous!", both MsgBox 64 titled "Search", and then the box's text is selected. The
    Enter goes no further (txtSearch_KeyPress). An empty box matches every entry, so it is "ambiguous!" unless the
    list has one line. A whole name that starts another ("Orc", "Orc Lord") is ambiguous too, as in the original.
    `Mordor.Core/LibrarySearch.cs` holds the search, and check.sh tests one, none and several.
  - **Exit** is "Exit", with no access key, and its Cancel is True (property 14 = FF), so Escape presses it
    anywhere in the form. The port's strip had given it Alt+X, which the form never had. The port's Escape had
    worked only while the focus was inside the page; it now goes through the window's key preview, as the Guild's
    Default and Cancel do, so it closes the Library whenever the Library is the active window.
  - **picMonster has no border.** Its record carries 24 = 00 after the rectangle, as do the other picture boxes
    that are drawn flat (frmExchange's picScroll and picItem, the automap's picMaze); the About box's framed
    picPicture has none. That this is BorderStyle 0 is read from that pattern, not from a table of VB3 property
    numbers.
  - The list is given the focus on opening, as optOption_Click gives it; txtInfo's text is kept clear of its scroll
    bar, which the theme lays over the text.
- **frmGuildLog.** lstLog 90,75 7620x1980, MS Sans Serif 8.25 plain; lblEntries 90,2115 1560x225; Clean
  ("&Clean Guild Log", Visible False in the form) 4305,2125 1755x315; Done ("&Done") 6150,2125 1560x315. Done is
  Alt+D and neither Default nor Cancel, so Enter and Escape do nothing, as in the original. The port's strip had
  given it Alt+X. Clean's Alt+C is the log's own while the log is the active window (since 2026-10-19), and Alt+C is the
  town's Confinement only while the town is. A clean now redraws the log in place, as sub316B does, instead of opening the window again.
- **frmHall.** lblLabel(1) 195,45 7305x330; elaElastic(1) 195,390 3510x4200 with lblRecordStat(0) 60,15 3450x240
  and lblRecordChar(0) 30,255 3435x435; elaElastic(2) 3930,390 3555x4200 with lblHistoryStat(0) 60,15 3465x240
  and lblHistoryChar(0) 60,255 3435x435; each later pair loaded under the one before (FRMHALL.BAS:115-119); Exit
  ("E&xit") 5415,4665 2085x345. Exit is both Default and Cancel (13 = FF, 14 = FF): Enter and Escape press it, as
  well as Alt+X. The port's note that Exit runs off the bottom was wrong: it ends at 5010, inside the 5055-twip
  client. The port's window had been 503x330 pixels.
- The town's own test page for the Hall (MORDOR_TEST_PAGE=hall) was the port's two-column page; it now opens frmHall.

DEVIATION: frmLibrary, frmGuildLog and frmHall call sub0802 on loading, which scales a form up by the screen's
size over 800x600, never down (MAINMOD.BAS:2026-2047). The port draws them at their own size, as it does the six
services. sub0802 also sizes every control's font from the Options window, which the port does (2026-09-28, "The
Options fonts are applied", below): it never renames a face, and a control with no ":" Tag takes NonProSize, so
frmHall's heading, designed 12 points, is 8 points bold (11 pixels) with the defaults.

**The main menu is frmMenu, complete - corrected 2026-09-26.**
- **Layout.** The eleven buttons stand where the form's resources in MORDOR.EXE put them, with their access keys and
  Image1's own bitmap behind them. The window caption is empty because frmMenu has none.
- **Change Password** (FRMMENU.BAS:42-67): the old password, then the new one twice. It keeps two faults:
  - cancelling the second prompt counts as a mismatch, and the loop goes round again;
  - the new password is stored exactly as typed.
- **Exchange Items** (FRMMENU.BAS:68-87): the other character is picked from the roster and loaded, with its
  password if it has one. It is refused if either character owes the bank or they are not in the same place.
  Otherwise frmExchange opens as its own window (FRMEXCHA.BAS). The other character is saved even after Cancel,
  so a swap followed by Cancel can duplicate or lose an item: the original's.
- **Hall of Records, About... and Credits** are frmHall, frmAbout and frmCredits.
- **Exit** asks "Exit Mordor?", then shows "THANK YOU FOR PLAYING MORDOR!". The source's `End` in mdrEndProgram
  (MAINMOD.BAS:947) reads as if it would stop before that box, but the original was run and shows it.
- **Loading.** A character that cannot be read loads silently: fn04CD returns only -1, 0 or -2, so the port's
  "Unknown error N" box could never have appeared. Unloading saves the character, as mdrcharUnload does.
- **Roster order.** The lists follow the roster's record order: creation order, with a deleted character's record
  reused by the next one made (fn04ED). The port keeps that order in roster.json beside the saves.
- **Transfer Gold** reads its amount as CLng does: &H and &O forms, exponents, halves to even. A negative amount
  runs the transfer backwards, as in the original.

**Gives, the vault and the drag targets - corrected 2026-09-26.**
- **"Give of Item to "X" Completed!"** follows every successful give, pack or vault, in town and dungeon
  (MAINMOD2.BAS:3004-3006). The port had said nothing.
- **A re-read on a give** files the item in the Library as sub0730 does: fully known 4, Mostly 3, else 0, grade
  only rising. The port had marked it fully known.
- **Refusals.** A refused give is a Messages line, and a full pack is a SORRY! box, in the dungeon as in town.
- **A vault line dropped on a party member's name** gives it to them (MAINMOD2.BAS:2972-3002). The whole record
  moves: charges, alignment, the equipped and cursed flags. A guild-issued item is refused. A full pack gets
  `Name has no free slots!`, with the name neither quoted nor trimmed, unlike the pack give's. There is no
  combat, self or equipped test.
- **A guild crest already in the vault**, not just the pack, means the guild awards none (FRMCTYG.BAS:698-721).
- **Any slot can be dragged,** the empty ones and the hands included (FRMCHAR3.BAS:589-593):
  - onto a buffer it empties the buffer;
  - onto the store it names the slot;
  - onto a full vault it is "Out of room!";
  - elsewhere it is silent.
- **Two drop targets on one control.** A drop no longer stops at the first target of the wrong kind. The first
  one had been swallowing drops meant for the second: a vault line on the pack list, an item on a party row.

**Non-damage spells follow mdrcharCastSpell - corrected 2026-09-26.**

**Casting from a spell buffer** goes the way a Cast from the sheet goes (MAINMOD2.BAS:4151, 6129):
- the checks first: knowing the spell, spell level, stats, points and antimagic (MAINMOD2.BAS:437-497, 830-835);
- then the effect;
- map effects (Teleport, Displacement, Passwall, Detect Rock and the rest) are cast, not treated as heals;
- when engaged, the spell becomes the order.

The port had skipped every check from a buffer, and sent map spells into the heal code.

**A refused spell** leaves the order at None. On an antimagic square it leaves Fight, or Defend when there was no
spell (MAINMOD2.BAS:433, MULTIMOD.BAS:467-479).

**Cures** clear the ailment's level as well as its flag, so a cured character can be poisoned or diseased again.

**City entry and death clear the spell resistances** with every other ailment (sub048C). Deaths from a companion's
mishap or a chest trap reach only mdrcharSetDead, and keep them.

**Levitate, Sight Veil, See Invisible and Protection** set their state and nothing more. Levitate's See Invisible
waits for the next recompute, and Protection keeps its spell value until then.

**Depth Perception and Find Direction** end being lost, for the whole party.

**Detect Rock** writes the automap: rock ahead unless you are lost; otherwise it marks the square visited.

**Ethereal Portal into rock** follows the slot loop (MAINMOD2.BAS:1185-1202):
- a caster in slot 0 or 1 kills the whole party with the rock death, then says "You walked into solid rock!" and
  marks the square;
- a caster in slot 2 or later kills the caster and those after them. The original then hangs in mdrcharSetDead's
  bare Resume (MAINMOD2.BAS:4586-4587); the port stops instead. That is the one deviation.

The portal's sound plays at the start.

**The same hang behind a deadly chest trap, and what avoiding it costs** (recorded 2026-10-18; the code's own comment
had it, this file did not). After a trap, monOpenChest prunes the party and then tests the leader's hits; below 1 it
calls `mdrcharSetDead pv04F2` - the opener's slot from *before* the prune (MONSMOD.BAS:3654-3658). When the opener was
not in slot 0 and one character is left, that slot is gone and mdrcharSetDead's bare Resume loops for ever. The port
sets nobody dead there instead. On the `o` key fn1521's tail (MAINMOD2.BAS:4089-4092) then finds the survivor below 1
and runs sub0930 on them, so they still die - but, by the port's own account at that point
(DungeonWindow.axaml.cs, `OpenChestBy`'s trap branch), with the death counted twice; the count-down open leaves them
at nought for the four-second tick's death check. A necessity (the original hangs), with a possible miscount noted.

**Retrieve Soul** offers only characters killed in rock on your level, and **Locate** everyone but the caster.

**Items:**
- they cast at their own spell level;
- a negative level casts at the guild's level;
- a level-0 item is gated and charged spell points;
- an item holding a map spell casts it.

**Sanctuary** ages the caster 365 days (mc0282) with mdrcharAddAge's "You are now N week(s) and N day(s) older."
line. The port had 28 days and no line.

**A chest trap that kills:**
- the count-down open ends at mdrcharSetDead;
- the 'o' key adds fn1521's ending (MAINMOD2.BAS:4089-4092): a second death counted, and sub0930 on slot 0, which
  lets companions and the carried body go.

**Faults kept:**
- Detect Rock's two wordings, and its visited mark even when you are lost.
- Cure Paralysis frees the caster, and fails in a fight.
- A bind that has no effect is still charged.
- Protection cast is worth half what the next recompute gives.
- "Anns Strength", without an apostrophe.
- The death count going up twice on the 'o' key.

**Encounters are fn0D4D, label by label - corrected 2026-09-26.**

The generator follows fn0D4D (MONSMOD.BAS:1361-1989), drawing its random numbers in the source's order.

**No encounter at all:**
- on a rock square (1432);
- in area 0, the only room gate.

The port's "the room you arrive in never holds anything" rule had no source and is gone.

**Choosing the lead:**
- The lair roll comes first, and is drawn again on every restart.
- Then the class, from the square's spawn mask. A 1% roll picks any class at all.
- Then the monster within the class, by rarity, over a floor window that widens by one each time nothing is found.
- On level 1, in areas below 18, a lead above guild level 2 is rejected unless it is table index 4 (1646). The 1%
  roll is suppressed there too.

**The companion groups** follow the lead's CompanionType and CompanionSpawnMode:
- modes 0-2 are floor windows;
- mode 3 repeats the previous group's monster;
- modes 4 and above name a table index;
- the subtype must match, until 150 tries, and the encounter is abandoned after 300;
- each group chains off the one before it;
- a companion window with no weight restarts the whole encounter.

**Each group** has one hit-point value, with no floor, and one misidentification roll.

**Faults of the original, kept:**
- **The class weights.** The loop adds v(0) where it means v(k), so each class is tested against the weight of the
  one before it, and anything left over goes to the last class. A class weighing 80 comes up about 10% of the
  time, and the class of weight 10 after it about 80%.
- **The empty fallback** can land on class 0.
- **Mode 7** keeps the previous window.
- **Stale slots.** Extra Foes slots from an abandoned attempt keep their data, count toward the gold, and show.
- **The chest lock** is never cleared on a restart.
- **A 1-hit group** comes out with 0 or 2 hits.
- **Joining.** An encounter of more than one group never offers to join, because the offer is wiped on every
  group.

**When everyone joins, the room counts as cleared,** as sub0C53 has it. The port had left the room standing, so the
same companions could be collected again.

**Respawn** follows sub0A91: t = 1200 + 150 x level - 5 x (the level's area count - its tally), at least 120
seconds, and live foes older than 2t are thrown away.

**Deviations:**
- **Rooms were regenerated, not stored** - corrected the next day: rooms are stored as the original stores its Foes
  (fn0E38/fn0F88); see "Stored rooms" just below. What is left of this is the next line.
- ~~**Members' hit points** are rolled when the encounter is made, not as each steps forward (sub0D08).~~ Corrected
  2026-10-19, see "The members behind a group's front" below.
- **Two unreachable data paths** return early. The shipped data reaches neither.

**Rooms keep their monsters; aging speaks; the store's small rules - corrected 2026-09-26.**

**Stored rooms.** Each area's Foes and Fight records are stored, as the original stores them, and are no
longer regenerated from a seed. fn0E38 reads them in sub0A91, and fn0F88 writes them back at these points:
after generation, when you leave the area, when the room is cleared (sub0C53), after the gold (monGetGold), and
after a chest trap or an opened chest.

**A room is on disk as soon as it is made - corrected 2026-10-25.** Each fn0F88 is a Put to the level's file, and
the port writes dungeon.json at each of them - all but generation, which it kept in memory until the next change of
area, so a game killed in between lost the room: in testing, an unopened chest. fn0D4D writes as it makes the room,
sub0C79's cleared records first (MONSMOD.BAS:1425, 794-803) and the new ones at its end (fn0F88, 1979), and the
dungeon window now writes dungeon.json then (`EncounterGenerator.InRoom`'s `written`). A room read back as it stands
writes nothing, as sub0A91 only reads it (687). The file is written beside and moved over the old one, so a kill
part way through a write keeps the last good file. Still the port's: when fn0D4D fails ("TryNumber > 300!") the
file holds sub0C79's cleared records, where the port stores the part-made room - the records sub0A91 goes on with
and the next fn0F88 writes anyway. `selfcheck` "the level's file written".

**What that means in play:**
- A group you walked away from keeps its losses and its front member's wounds.
- An emptied room shows its unopened chest when you come back.
- A fight is tied to its own area, so crossing into another area mid-fight cannot book the win there.

**Free rounds and timing:**
- Monsters get their free round only on a change of area: sub0A64 2 as you leave an engaged group (MAZEMOD:811-818),
  and sub0A64 1 as you arrive at one (1468-1470). They get it at the step, not on the next tick.
- A chest shows as soon as you arrive, even while its group only waits. The DEX test hides only the trap panel.
- Opening a chest with a group waiting starts the fight quietly first.
- The Asmodeus question comes first when a room is cleared (sub0C53).

**Deviations:**
- ~~The members behind a group's front are rolled again each time the room is read, since only the front one's
  record exists.~~ Corrected 2026-10-19: see the next entry.

**The members behind a group's front get their hits as they step forward - corrected 2026-10-19.** The original
keeps one record per group, the front member's; the members behind it are only its count. When the front falls,
the routine that took it off the count calls sub0D08, which loads the record afresh - the creature's hits, then
`Int(Rnd * 2)` for the sign and `Int(Rnd * (Hits * .5) / 2) + 1` for the amount (MONSMOD.BAS:1991-2008). The port
had rolled every member as the encounter was made, after fn0D4D's own draws, and again on every visit that read the
room back, so the draws came in another order and number. Now a member behind the front is `Unrolled` and draws
nothing until the engine calls `Sub0D08` where the original does: a character's killing blow (MONSMOD.BAS:497), a
group spell's kill or charm (MAINMOD2.BAS:1520-1521), a companion's spit (MONSMOD.BAS:1024), blow (1180), stone
(1249) and shock (1296), a thief running off (3382) and monsters joining (MAINMOD2.BAS:4110-4111). sub0D08 loads
the record of the group the character is aiming at, `Foes(CombatOrders(who).M547A)`, which is not always the one
that lost a member, and that is reproduced:
- **A companion's breath** sweeps every group (MONSMOD.BAS:925-952), but each kill loads the owner's target group
  (948). In another group the record stays at nought while its count goes down, so after the first kill there
  every member it reaches dies at the next breath, whatever the breath does, with no experience for it, and each
  such kill gives the target group's front fresh hits, wounds and all thrown away.
- **A thief that runs off** (3380-3382) leaves its group's record as it was for the one now in front, and loads
  the robbed character's target group - M547A itself: when that group has gone, sub0D08 finds its count at nought and
  draws nothing (1995, 2009), and the port no longer falls back to the first group standing there. A thief's group
  emptied instead moves the robbed character's aim off a group that has gone (3384-3386).
- **Monsters that join** (MAINMOD2.BAS:4102-4111) all take the front's hits; the first group's record then keeps
  those hits for the one now in front, unless the one in command aims at the first group, which is then loaded.
- A character's further swings stay on the group aimed at while it stands, "If Foes(M547A).GroupCount < 1 Then
  M547A = fn0DCD(-1)" (MONSMOD.BAS:509-511); the port had turned them on the first monster standing as soon as the
  aimed-at member fell.
- The aim itself moves as the original's does: before a character acts, off a group that has gone
  (MONSMOD.BAS:303-305); after a killing swing (509-511); and for everyone after the characters' turns, before the
  monsters go (MULTIMOD.BAS:542-551).

**The record at nought - corrected 2026-10-19 (review).** The record can stand at nought with its count above nought:
sub0D08 or fn0D4D rolls a 1-hit creature (the Gra&#232;l Mosquito, the Piranha) to 0, which then stands until hit; and
where a member is taken off and nothing reloads the record - a breath's kill in a group not aimed at (MONSMOD.BAS:946-948), a charm
with no companion slot free (MAINMOD2.BAS:1514-1516) - the next member steps up onto a record at nought. That member
falls to the next thing that writes its hits, a spell, breath or spit of nought included (MAINMOD2.BAS:1478-1492,
MONSMOD.BAS:936-944, 1016-1021); a weapon's miss writes nothing (440-442) and leaves it standing, and a companion's
stone and shock are not thrown at it ("Foes(M547A).Hits > 0", 1215). The port had no member alive at nought: the
breath set its members to 0/0 and left them dead, and the room's write-back counted 0/0 as a front "rolled at
nought", so a won room was saved with its group standing at base hits and never refilled. Now a member carries the
state (`CombatMonster.AtNought`, apart from `Unrolled`, not yet rolled): it is alive, any write of its hits ends it,
the write-back counts the living (`Alive`) and stores the record's nought, and a front read back at nought stands
again. The breath's cap of five kills and the charm leave the next member at nought for whatever comes after.

A list built by hand (the probes, no slot) keeps the hits it was given.
- The ASMO cue plays after the encounter sound, and the two overlap, as the original's WaveMix overlapped them (since 2026-10-21, "Sound effects: WaveMix's eight channels").

**Aging.** mdrcharAddAge's "You are now X older." / `"Name" is now X older.` line (MAINMOD2.BAS:6401-6405) now
comes with:
- monster aging, followed by its own "is now older!";
- chest Withering;
- every raise;
- a Kind 25 stat item, which ages you 28 days (6228), reversed or not.

**Resting and rescue:**
- The dead are rested with the living in the city: 31 days of age, full hits and spell points while still
  dead, and a store turnover if the dead one is member 0.
- The rescue is a Beep and a "You have been found after N day(s)!" box; the port's "...older." text is gone.
- For member 0 the rescue turns the store over with 0 days, which becomes a 1% chance: the original's.

**The General Store:**
- Zero-charge items of the chargeable kinds combine (FRMCTYS.BAS:662-668).
- The poured-away slot keeps its other fields.
- Starting stock tests Align > 1.
- The Library is filed only on identify and uncurse, at sub0730's grade.
- Combining past 32767 charges is VB's untrapped Overflow: the runtime's box, and the game ends without saving,
  as the original does.

**Two queue batches: combat orders, deaths mid-fight, items as orders, typed names, and the city's small rules - corrected 2026-09-26.**

**Combat orders**
- The order glyph shows while monsters only wait, as monShowCursor asks only whether monsters are present
  (MONSMOD.BAS:4198-4229). The waiting rows read "n. count name".
- A fight's start gives the Combat Setting only to those with no order, keeping the aimed group (sub0BDE,
  MULTIMOD.BAS:158-165).

**Deaths mid-fight**
- Every monster group re-reads the party. A member killed mid-round goes through sub0930 at once: companions
  let go, ailments cleared, saved and pruned.
- The loop ends only when one member is left (MULTIMOD.BAS:305-364), so a lone survivor of two escapes the
  remaining groups that round. That is the original's.
- The port had never pruned members killed mid-fight.

**Items and spells in a fight**
- An item used in a fight becomes the order (MAINMOD2.BAS:6129-6137). It is cast each round and spends its
  charges. "Your item has run out of charges!" and Fight follow.
- A spell cast over an item order keeps draining the item (M5766 is not reset, FRMCHAR3.BAS:180), and each
  refusal clears or keeps it where the original does.

**The dungeon's own details**
- Opening a chest tests only death.
- The arrival and engage encounter sounds had no source and are gone.
- The empty panel hides after a chest is opened.
- The level-1 stairs mid-fight leave the room as last written.

**The spell box.** Cast, Enter and a double-click read the typed spell name through fn1725
(FRMCHAR3.BAS:150-186, 617-620). Turning logs only mdrmazPerceiveAhead's finds.

**The city and core**
- Companion Info files the creature in the Library (sub06FE), with the place below ground.
- The name picker is fn170D's drop-down, remembering its last pick (m001C).
- The Seer's carrier sentence is cut as VB cuts it.
- The Store's scrollbar is disabled while an alignment is chosen.
- The store's "someone bought one" restock checks only the unaligned count (MAINMOD.BAS:1640-1646): the
  original's.
- The identify skill reads Stats + StatMods (MAINMOD2.BAS:2511-2512).
- The item check before a Kind 25 item's aging runs (6227), testing guild, level and stats as fn11B6 does.
- The Messages list no longer drops a repeated line: sub085B does not.
- The unreached city character sheet is gone.

**The last queue batches - corrected 2026-09-26.**

**Dungeon and core**
- **The dungeon's status line** (HP, SP, Gold, Exp) had no source (dmainw1.png) and is gone. The party and
  character windows show all of it.
- **Every character stores its area** (M59BB), written where the original writes it. Rock deaths leave 0.
  Pickups, raises, joins, Fetch, the Look page and the Exchange location check all read it.
- **The chest's teleporter trap** moves only the opener (MONSMOD.BAS:3559-3602). In a party the opener drops
  out of it, and the rest keep the chest, untrapped and full.
- **An item buffer can be a Combat Setting** (sub0BDE → sub15CD). sub0BDE runs at the original's call sites.
- **A won fight** blanks the monster pictures (MONSMOD.BAS:780-781).
- **The spell list** holds only the character's own spells. Items are used from the pack and the buffers, as in
  the original.
- **Experience needed** and the level cap are fn12A1 and fn13ED line for line, in Currency. 12 of the 16,800
  guild, race and level cases to 200 differ by one from the old closed form (269 before the x87 correction below).
- **The window's close box** is refused: "Please use the Mordor EXIT buttons to leave the game."

**Options, Help, menu and city**
- **Backup and Restore** are fn0662 over the saves directory:
  - a backup mirrors the characters exactly;
  - a restore copies first, and then removes only the characters the backup lacks;
  - shared files are never deleted.
- **The font "..." buttons** open the font dialog. The Pro and NonPro keys are the right way round again, and are
  named the wrong way round, as in the original.
- **MORDOR.INI.** The original's MORDOR.INI lived in the Windows directory. The port keeps its settings in
  settings.ini beside the saves, and only reads the game folder's MORDOR.INI as a fallback.
- **The first-run "Welcome to Mordor 1.1!" box** is shown once.
- **The Help window** carries the help's own title.
- **Guild items.** An item not tied to one guild can be used by any guild the character has joined (fn11B6).

**A spell as the Combat Setting, and the city's Area list - corrected 2026-09-26.** A spell set as the Combat
Setting is checked as the fight begins (MULTIMOD.BAS:176-183). If refused, the character takes the check's own
outcome and then fights (sub102F). The city's Area list compares each character's stored area with the
leader's (MAZEMOD.BAS:280-292), instead of taking everyone to be in town.

**The final audit's findings - corrected 2026-09-26.**

A sweep of every string in the port was made against the source, the help, MORDOR.EXE, the data files and the
screenshots.

**Removed, having no source:**
- the city's row of text buttons;
- the toolbar and automap tooltips;
- "You wade through cold water." and "You struggle through quicksand.";
- "Under:" and "Protected N";
- the crash-log line on the Wait button;
- the fallback guild and spell-school tables, and the loader's invented clamps.

**Now as the original has them:**
- **Window captions.** The frames are "Mordor 1.1", and the Dungeon child is "Dungeon" when one character is
  loaded.
- **Cast.** The button is &Cast.
- **Wading.** WADE plays for water and quicksand unless you levitate.
- **A missing MDATA1** gives the original's "ERROR!" box.
- **The level fee** is fn12B2 over the guild data, evaluated as the x87 evaluates it (see the x87 scan). 1,130 of
  11,976 guild-levels come out one gold different from the community page's loop, through the last line's `Gold / 10`.

**The spell page** follows FRMCHAR3's layout and sub2424's rows:
- Spell Type and the class box;
- Spell Name / Guild / Lev / Points, with "***" past 999;
- bold when castable, and "No Spells Yet" for an empty class;
- the combat spells no longer hidden outside a fight.

**Deviations:**
- **The spell page** is a scrolling list, and the selected row gets a highlight background rather than blue text
  (QBColor 9). (The class box kept the class chosen when sub1536 refilled it for the same character; corrected
  2026-10-19, see the next entry.)
- **The Dungeon child's caption** follows whichever routine last wrote it, as in the original: see "The Dungeon
  caption" below (no longer a deviation).
- **The help window.** Its topic list and search box stand in for WinHelp's Contents and Search. Their words
  ("Search", "No match", "Help unavailable") are the port's own, because the original's help button hands
  everything to WinHelp (FRMMAIN.BAS:75-76).
- **Access keys.** &Cast and &Info are Avalonia access keys, with the underline shown while Alt is held.

**Listing the spells drops a Fight or Defend order - corrected 2026-10-18.** This was filed above as a display
matter; it is a rule. mdrcharCheckCast opens with `CombatOrders(pv00FC).Att = False` (MAINMOD2.BAS:433), before
any test and whatever its last argument, and the spell page calls it for every spell cmbClass_Click lists
(FRMCHAR3.BAS:112) and every row sub2424 writes (FRMCHAR3.BAS:699, from vsbSpells_Change :912 and cmbClass_Click
:142). Fight and Defend are `Att` (with `M56C4 = -1`, MAINMOD2.BAS:3602-3604, 4030-4032), so picking a class,
scrolling the list a row, or anything that runs sub1658 - sub1536 (MULTIMOD.BAS:675-677) and an item's stat change
(MAINMOD2.BAS:6230) - leaves the window character (Me) holding None, and the timed round does nothing for them until
a new order. A queued spell (`M56C4`) and the aimed-at group stay. The port's `Spellbook.ShownBold` had no side
effect; `CombatOrder.CheckCastClearsAtt` is it now, run by the dungeon's `SpellListChecked` from those places only
(the port's other repaints of the list are its own and leave the order alone), and only when a spell is listed -
"No Spells Yet" checks nothing. Only a scroll the player makes counts - the wheel, a key, the bar's arrows,
track or thumb; the bar's moves on a refill, a resize, a clamp or the whole-row snap do not. The window closed with
its close box and opened again is a fresh Form_Load, whose sub1658 (FRMCHAR3.BAS:382) lists the first class and
checks it. The city's page is left alone: no order is standing there (FRMGROUP.BAS:419).
`selfcheck` asserts the clear and the item test.

**The Spell Type box goes back to the first class where the original empties it - corrected 2026-10-19.** sub1658
fills cmbClass only when it is empty and then picks the first entry; otherwise it re-clicks the class already
chosen (MAINMOD2.BAS:5516-5537). Two routines empty it first: sub1536, every time it runs, with txtSpell emptied
too (MULTIMOD.BAS:675-677), and the Guild's Join, for a guild joined or re-entered (FRMCTYG.BAS:163-164). So a
double click on a name in Party Information, Switch To or Alt-S onto a character the windows do not show, Objects &
Spells or the character window activated after a prune, and every switch in town (mdrmulSwitch ends in sub1536
there, MULTIMOD.BAS:638) list the first class again and leave the spell box empty - for the same character too. The
port had kept the class chosen whenever the character and the classes were unchanged, and had kept the typed spell.
`SpellPage.Clear` is the cmbClass.Clear; the dungeon's `Sub1536`, the town's `SwitchedTo` and the Join call it.

In town a click on the name already in command, Alt+1..4 for that member, or a companion picture dropped on it are
no switch at all: lblName_Click is `If Index <> Me Then mdrmulSwitch Index` (FRMGROUP.BAS:175-179) and lblCall_Change
tests the same (150-151). The port had run the whole switch for them, telling the open services "UPDATE"; they now
do nothing (`SwitchIfOther`). Selfcheck "UI behaviour: the Spell Type box is emptied where the original empties it".

**The character window is restored where mdrcharSetInfo restores it, and only there - corrected 2026-10-19.**
mdrcharSetInfo opens with `If frmCharStat1.WindowState <> 0 Then frmCharStat1.WindowState = 0` (MAINMOD2.BAS:4598):
a minimised window and a maximised one alike come back to their normal size, and a restored MDI child is the active
one, so it comes to the front. A window already normal is left where it is in the stack.
- **A window restored from an icon is the active child - corrected 2026-10-25.** Measured in the original (Ctrl+L,
  below): the minimised character window came back as the active, topmost MDI child, as Windows' restore makes it.
  The port's `MdiChild.SetNormal` had brought it to the front but left the active child (`MdiHost.Active`, the form
  the keys go to first) as it was, so the town kept its Alt pictures and a service its keys. It now makes the window
  the active child too, which in the dungeon is Form_Activate's "If M5E4F <> Me Then sub1536 Me"
  (FRMCHAR1.BAS:33-37): `MdiHost.Activated` raises it on any change of active child, where it had hung on a click
  alone. A maximised window comes back to its normal size and to the front as before.
- **Every Info** - an item's in the pack, the Store or the Bank, a spell's, a companion's, the Confinement's - is
  mdrcharSetInfo and then `frmCharStat1!picTabPicture.SetFocus` (FRMCHAR3.BAS:208-209, 298-300, 758-759,
  FRMCTYS.BAS:165-166, FRMCITYB.BAS:80-81, FRMCITYC.BAS:201-202), so the window is restored and brought forward.
  The port had restored only a minimised one; a maximised one now comes back to its size too (`MdiChild.SetNormal`).
  A SetFocus on a form's control activates that form, at its normal size too, so since 2026-10-25 the character
  window is also made the active child (`MdiChild.Activate`), and the keyboard leaves a service's box for it: an
  Info pressed in the Store, the Bank or the Confinement leaves that form's keys waiting for a click on it, as the
  original's service form loses the activation to frmCharStat1. The pack's, spell's and companion's Info already
  moved the keyboard.
- **F2, F4, F6 (and Shift-F6) and F7** are mdrcharSetInfo with no SetFocus after (MAINMOD2.BAS:3534, 3544, 3552-3555,
  3559): restored if minimised or maximised, and otherwise not raised. The port had raised it every time.
- **F3 and F9** set tabStatTab and send lblCall "UPDATE" (3535-3538, 3564-3567), and **Ctrl+S, Ctrl+B and Ctrl+M** set
  it in fn15DF (4193-4201): the page changes and the window is neither restored nor raised. The port had done both.
- A window closed with its close box is loaded again by any of these, as a reference to an unloaded form loads it.
- **Settled: Ctrl+L, Ctrl+R, Ctrl+C and Ctrl+G (2026-10-21; measured 2026-10-25).** Their tabs (4, 1, 2, 3) are the
  ones tabStatTab_event0000 sends through mdrcharSetInfo (FRMCHAR1.BAS:322-335), so they restore the window if
  setting the tab from code raises that event, and not otherwise. The control is VideoSoft's Index Tab, `VSIndexTab`
  in game_files/VSVBX.VBX, whose own strings list one custom event, `Switch(OldTab As Integer, NewTab As Integer,
  Cancel As Integer)` - the form's event000D, which the decompiler names TabChange in its error text
  (FRMCHAR1.BAS:343-393) - so event0000 is the standard Click. Whether assigning `CurrTab` fires Click the VBX does
  not say in anything readable (it imports nothing but KERNEL, GDI, USER and WIN87EM), and the game's own code
  pointed both ways. The running original (under OTVDM, from a scratch copy) settles it: **assigning CurrTab from
  code fires the Click.**
  - Ctrl+L on a minimised character window restored it, and made it the active, topmost MDI child.
  - Ctrl+S on a minimised window left it minimised (the control: tab 0 is not among those event0000 sends on).
  - Ctrl+C on a normal-size window behind the city changed the tab but did not raise it: the city stayed active and
    on top, as no SetFocus follows.
  - Ctrl+G on a maximised window restored it to its normal size, on the Guild tab.

  The port already restored a minimised or maximised window for these four keys (the F-keys' SetInfoTab) and left a
  normal one where it was; it now makes a window restored from an icon the active child as well (above). fn15DF is
  the same assignment from a service's text box or combo box (FRMCTYS.BAS:413 and the rest), so Ctrl+L, R, C and G
  typed there restore the window too - the port had only set the tab - and when the window came back from an icon,
  and so took the activation, the box loses the focus; otherwise the box keeps it, the window is not raised, and
  Ctrl+S, B and M there still only set the tab. Whether an assignment of the tab already shown fires the Click was not
  part of the measurement (mdrcharSetInfo assigns only a different tab, 4618-4622); the port restores either way.
  The Click's own first lines, "If M5E4F <> Me Then sub1536 Me" (FRMCHAR1.BAS:323-326), and the Switch event's
  (348-350), which matter only after a party is pruned to one, are not modelled on a tab change.
Selfcheck "UI behaviour: the character window and its page keys", and Mordor.UiCheck's town drives the four measured
cases and Ctrl+R from a service's box.

**No invented fallbacks; "No Spells Yet" is disabled - corrected 2026-09-26.**
- **No fallback tables.** The race and guild tables have no built-in fallback. A missing or unreadable MDATA1 is
  the original's ERROR! box.
- **Unknown names in a save.** A saved character whose race or guild is not in MDATA1 is unreadable, and is left
  out silently, as fn04CD's 0 is (MAINMOD.BAS:349-351), rather than failing in play. Backup still copies the file.
- **"No Spells Yet".** An empty spell list disables elaSpells (FRMCHAR3.BAS:723-727), so its "No Spells Yet" row
  cannot be clicked. P?C00F is property 0xF of VSVBX.VBX's VSElastic model, whose list at DS 0x139e has
  IPROP_STD_ENABLED there.

**Teleports and teleporter squares, rock and the loader - corrected 2026-09-26.**
- **Nothing moves you off rock.** The port had nudged anyone placed on rock to the nearest open square. No source
  has that, and it let a Teleport into rock pass unharmed.
- **The spells.** Teleport and Displacement decide before anyone moves (mdrcharTeleport, MAINMOD2.BAS:5839, 5860):
  - level 0 or below is a fall;
  - rock, or a level past 15, is the rock death, with the bodies left on that square and level.
  Ethereal Portal steps, wraps and then tests (1170-1184), so its bodies lie on the rock.
- **Teleporter squares** follow MAZEMOD.BAS:1788-1830, not mdrcharTeleport:
  - no fall and no rock death;
  - a fixed square moves everyone to its destination;
  - a random one draws a square for each member separately, never rock or another teleporter, on the leader's new
    level after the leader;
  - anyone not on the leader's square then "has disappeared from view!" and leaves the party (sub0908). So a random
    teleporter all but always splits a party: the original's.
- **The race and guild loader** keeps every record, as fn03D0 does. The port's name and range filters are gone.

**A square's effects, perception and captions follow sub0835 - corrected 2026-09-26.**
- **Order.** A step runs the square's effects in the source's order (MAZEMOD.BAS:1703-1896): water and
  quicksand, the extinguisher, the facing bits and then the rotator (so a rotator's spin wins), each member's
  extinguisher and then the pit, then the teleporter and the chute. The port had handled the teleporter first and
  skipped the rest, let a fixed facing override a rotator, and put the pit before the extinguisher.
- **Perception.** Each bit is perceived just before its effect, by the leader, as mdrmazPerceiveSquare does
  (620-670). A teleporter is always noticed, "You just hit a teleporter!", and goes on the automap unless the leader
  is lost. "Completely certain" is said only when the bit is mapped.
- **The second pass.** A teleporter or chute runs the whole square routine again on its destination (l0286,
  1891-1893). A destination that is a pit or water acts at once; the shipped maps have no teleporter loop.
- **Getting lost** is rolled on the leader, whose flags the party shares (1541-1583).
- **Captions and messages.** The square's caption is its label only. It is blanked every step and overwritten by the
  stairs over a pit, and it never goes to Messages. The sub085B lines go to Messages in the order said.

**Gear in perception and thieving - corrected 2026-09-26.** Perception reads natural WIS plus a quarter of what gear
adds (MAINMOD2.BAS:1962), and thieving reads WIS and DEX with gear in full (1945-1946). This applies both when
perceiving a square or what lies ahead and to every chest roll. The port had read natural stats only there.
Neither stat is floored at 1, since StatMods never is. The wrong guess of a square's feature redraws without
limit, as the original's loop does (MAZEMOD.BAS:653-661).

**Bugs from an on-screen playthrough - corrected 2026-09-26.**
- **A room's chest.** A chest belongs to its own room. Leaving the area writes it back there, and only there can it
  be opened (MAZEMOD.BAS:811, MONSMOD.BAS:687). The port had let a chest be opened from the next area, and its gold
  be paid twice.
- **Finishing a fight on the way out.** A fight won while its round is still being drawn is finished on every way
  out: stepping, the stairs, `q`, closing.
- **Arriving by spell.** Teleport, Displacement, Passwall and Sanctuary run the landing square's routine, and then
  mdrmazShowMaze (MAINMOD2.BAS:5899-5904, 1209-1214, 1348-1351). Sanctuary plays WITHER.
- **The automap:**
  - it prints the position 1-based (FRMAUTOM.BAS:550);
  - its level bar follows you down only with Auto Change Lev. (306-313).
- **The Spell button and the `c` key** open the Spells page with the spell box selected (FRMCHAR3.BAS:454-460).
- **Enter in the spell box** casts.
- **Keys while Objects & Spells has focus.** A text box, combo box or scroll bar keeps every key but Escape, and
  Escape returns the focus (FRMCHAR3.BAS:310-331). The window has no letters of its own: e, u, i, d and c are fn1521's.
- **Escape** no longer leaves the dungeon.
- **`q` and the Exit button** ask "Exit to Main Menu?", and are refused while monsters stand unless you are dead
  (MAINMOD2.BAS:3619-3631). Yes returns to the title with the character where they stood.
- **After a fight:**
  - gold on the toolbar updates as it is paid;
  - the party's orders reset out of combat (FRMGROUP.BAS:418-422);
  - an empty monster picture is hidden.
- **A fresh character's windows** are laid out by sub150A (MAINMOD2.BAS:3210-3244), from the frame's outer size.
- **A restore** also removes party files the backup lacks, as MDATA12 is copied whole.

**Leaving for the main menu** (`q`, the dungeon's Exit, the city's Exit) runs sub16CC (MAINMOD2.BAS:5914-5959). With a party it
says "Disbanding Party..." and asks `Do you wish to Reload this Party when "Name" is loaded again?` ("Save Party?", No the
default). Yes writes the leader's party record; No blanks it; a lone leader's is blanked without a question. The record
is written nowhere else (MULTIMOD.BAS:387-444), so a leader brings back the party they last left with after a Yes, and a
dead leader brings nobody (MULTIMOD.BAS:219). The city's Exit asks "Exit to Main Menu?" (FRMCTYM.BAS:214-219) and no
longer plays BYE.WAV.

**Running a character, reaching the city, and the second playthrough's fixes - corrected 2026-09-26.**
- **Running a character below ground** opens the dungeon on their square, not the city (sub14E0, MAINMOD2.BAS:3170-3182),
  and nothing rests. frmCityMain's Form_Load, with its rest and ageing, runs only on walking out or after a raise at level
  0 (sub14CC, MAINMOD2.BAS:3134-3155).
- **Form_Load** rests only the character run. The autoload follows it (MAINMOD2.BAS:3171, 6306), and compares members with
  the leader's level and M59BB of -1 (MULTIMOD.BAS:248). Nothing said during a run reaches Messages: gv1B52 is not yet
  set (MAINMOD2.BAS:6305-6308, 3198). The first line is "Welcome to MORDOR 1.1!" (MAIN.TXT:34). This supersedes the
  claim that every autoload outcome is reported by name.
- **The stairs out of level 1** set the leader's level to 0 (MAINMOD2.BAS:3956), and the city lines the party up on the
  leader (FRMCTYM.BAS:105), setting everyone's area to -1 (112, 132). The port had moved only the selected character.
- **Waiting out a rescue** does not rest (FRMDUNM.BAS:194).
- **A chest's gold share** is a Messages line.
- **The info windows** repaint on every character switch.
- **Keys.** Keys are previewed by every form (FRMCHAR3.BAS:310-331), so a list never keeps the arrows; Tab still moves
  focus.
- **A Windows session end** asks "Exit Mordor?" and runs sub16CC (FRMMAIN.BAS:176-192).
- **Preload Character** is the settings file's Character entry, written by sub16CC (MAINMOD2.BAS:5948-5953) and read at
  start (MAINMOD.BAS:1266).
- **The city** has frmCityMain's Alt-letter keys (FRMCTYM.BAS:35-58), and `q`.

**Deviations:**
- The dead are shown sub0930's screen on the city's panel, not in frmDungeonMain.
- Minimised windows become a bar along the bottom of the frame.
- The last-clicked window stands for the active form.

**The user's playthrough: saving, dying, the automap, creation, the Store, and one frame - corrected 2026-09-26.**
- **Saving.** The party is written whenever it enters a new area or level, as mdrmazShowMaze does (MAZEMOD.BAS:797-800,
  834-842); No Autosave skips only that write. Facing is kept in the record (MAIN.TXT:329), and every member gets the
  leader's (MULTIMOD.BAS:723). Window placement is kept per character across the town and the dungeon, and the dungeon
  writes its own on every exit (FRMDUNM.BAS:360-361).
- **The dead** are refused the keys of fn1521's second block (MAINMOD2.BAS:3757-4166). A lone pit death shows sub0930's
  label (MAINMOD2.BAS:3049-3055). The dead screen has no Exit (it moves by its title bar since 2026-09-27, below), and every town picture turns a dead
  character back to it (sub14E0, MAINMOD2.BAS:3166-3180).
- **Attack sounds.** SWING plays before the wait, and HIT and the death cry once each after it (MONSMOD.BAS:344-350, 452,
  471-473).
- **The automap:**
  - picMaze is never scaled. modAdjustMap centres an axis on you only while the form is smaller than 458x493 on that
    axis (FRMAUTOM.BAS:455-501). Re-examined 2026-09-28 against a screenshot of the original that seemed to show
    the map grown with its window: see [the automap is not scaled](#the-automap-is-not-scaled-re-examined-2026-09-28).
  - Fog on the square you stand on stops its edge writes and both neighbours' (MAZEMOD.BAS:1570, 1646, 1671).
  - West and south boundary walls are drawn beside any square you have stood on (FRMAUTOM.BAS:430-439).
  - Walking into a wall bit in fog records it (MAINMOD2.BAS:3864-3891).
  - The arrow blinks every 500 ms unless "No Automap Cursor Blink" is set, which is read on every tick
    (FRMAUTOM.BAS:321, 608-632).
- **Creation.** The Guilds Allowed list is drawn in the form's black on silver, where bold, plain and italic are the
  only marks (CHARCREA.BAS:346-379). The Store's "U G N E" is right-justified over the counts.
- **One frame.** The dungeon's windows are put into the city's frame in place of the town's, as frmMain swaps
  frmCityMain for frmDungeonMain (FRMCTYM.BAS:211-212, MAINMOD2.BAS:3957-3958).
  - The City is window 10: Fixed Double, not resizable, laid out to FRMCTYM.FRM (FRMCTYM.BAS:95-104, 145, 171).
  - The Dungeon is drawn to FRMDUNM.FRM: 480x203, a 101x101 view and its four arrows. It scales with its window, and
    its height follows its width.
  - Minimums are the original's: half the default size for five forms, and a quarter for the automap.
  - Objects & Spells opens by default (MAINMOD2.BAS:3160-3162).
  - Messages rows are one line high.
  - Every service, the Seer included, closes as you go down (FRMCTYM.BAS:169-179).
  - Alt+F4 reaches the "use the EXIT buttons" box.
- **Service windows.** Each opens at its own window-table entry's size and place (11, 12, 15, 16, 19, 5), or centred
  when never placed (sub02FE). It saves on unload, and is Fixed Double; only the Seer has a ControlBox. The Library
  and the Guild Log open over the Guild and return to it (FRMCTYG.BAS:170-177).

**Deviations:**
- Each screen builds its own set of child windows, and the layout passes between them through the character.
- Sizes are scaled against the largest 800x600-shaped part of the frame (CHARCREA.BAS:307-308).
- The town and dungeon scale as whole pictures.
- A service window is as big as the port's own page needs, and no bigger than the frame. It is exactly as tall as
  the page, whatever height its table entry holds.
- The frame window's own size, place and maximised state are kept in settings.ini (see below, 2026-09-27).

**Deviation, at the user's request: the Store's alignment choice does not lock the list - 2026-09-26; moved into the
Quality of Life mod 2026-09-27.** In the original, once an item stocked in more than one alignment is clicked, the
Select Alignment frame holds the store. Every other row only beeps, and the list's scroll bar and EXIT are greyed until
an alignment button is pressed (FRMCTYS.BAS:332-335, 358, 981). The game's default is that lock again, as it was
before commit 164befa: the other row beeps, the scroll bar, the mouse wheel and EXIT are dead, and a search does not
move the list. The unlocked behaviour - clicking or arrowing to another row withdraws the pending choice and takes the
new row, while the list, the search and EXIT stay usable and BUY and INFO wait for an alignment - now lives in the
Quality of Life mod (docs/MODDING.md), off unless that mod is on (since its 1.9.0; its own toggle before). It
is asked through the StoreAllowsRowChangeWhileChoosing decision when the frame comes up. The list's keys follow the
same decision, asked when the Store opens (corrected 2026-09-27): with it off - the game's default - the list takes no focus, as the original's
elaItem labels take none and every key goes to Form_KeyDown and on to fn1521/fn15DF (FRMCTYS.BAS:407-424), so the
arrows never move it. With the unlocked choice on, the arrows move the list and a row reached that way counts as a
click. Either way one mouse click is one elaItem_Click: the list's selection moving on the press no longer runs the
click a second time on the release, which with the lock on had put the frame up and then beeped at it.

**Help tables line up as WinHelp's did - corrected 2026-09-26.** The help keeps WinHelp's tab stops: each tab-stop span's
width, and RTF's default grid of 720 twips (half an inch) where a paragraph uses the default. Tabbed rows are laid out at
those stops in Arial 10pt metrics, and a tab past its stop goes to the next one. The port had flattened the stops into
two spaces, so tables such as Guilds: Statistics came out ragged. The text of every topic is unchanged.

**Deviation, at the user's request: the Store greys out items your character cannot use - 2026-09-26.** In the original,
usable items are bold and the rest are in normal weight, with no other difference (FRMCTYS.BAS:1017-1023; the help:
"Items that are displayed in bold are items that your character can currently use"). In the port's font the two weights
are hard to tell apart, so unusable rows are also drawn in grey. Which rows count as usable is unchanged.

**The Store's list stays where it was after a purchase or sale - corrected 2026-09-27.** cmdBuy_Click and cmdSell_Click
both end with `sub260A 1` (FRMCTYS.BAS:91, 285). That keeps the scroll bar's value in vsbItems.Tag and redraws the nine
rows from there, clamped to the new vsbItems.Max (548-552, 976-1026). The highlight stays on the chosen item's row
(1011-1016). The port rebuilt the list and jumped back to the top after every buy, sell, uncurse, identify, realign and
combine. Now the top row is kept, or the list goes as far down as it can when rows have gone, and the highlight stays.

**Service windows have no empty band under the page - corrected 2026-09-27.** frmCityStore and the other five service
forms are Fixed Double (BorderStyle 3), so each is exactly its design's size. The port made a service window the taller
of its page and its window-table height, and the table's height left a blank band under the Store's page. A service
window is now exactly as tall as its page at the width it opens at. The width still comes from the table entry, and the
dead screen still takes the Dungeon's size. The saved layout is unchanged in format. The width is held to the frame's
before the page is measured at it, since a narrower page wraps taller. The Store's Select Alignment frame (elaAlignments)
is part of the fixed form's design whether shown or not, so its room is kept while it is hidden; showing it cannot push
the page past the window's bottom. Superseded 2026-09-28: the six services are now drawn to their forms' data, and each window is exactly
its form's client (see "The town's service windows are drawn to their forms' data").

**The window layout is kept between sessions - corrected 2026-09-27.** The original writes the window table only when a
character is made (CHARCREA.BAS:302-343), when the Control Panel's Adjust to Display or Reset is pressed
(FRMCONTR.BAS:115-116, 149-150, 185-186) and as each form unloads; a saved table is read back as it was. The port had
wiped every table saved before its layout revision 1 and laid it out again as a new character's. That is removed:
WindowsRevision is still written, only so that saves carrying it load. A blank table is still seeded and placed by
sub150A, as CHARCREA does. sub0524 (MAINMOD.BAS:487-494) puts a window back to 0 only when its Left or Top lies past
Screen.Width or Screen.Height, the whole display. The port compared against the frame's own area, so a window saved low
in a tall frame jumped to the top when the frame opened smaller. It now compares against the primary display, in its
pixels over its scaling. SelfCheckProbe holds a table saved with Top 834 in a 725-tall frame unchanged.

**Deviation, a port necessity: the frame keeps its own size and place - 2026-09-27.** FRMMAIN.FRM is not among the
decompiled files, so how frmMain itself was sized is not known; MDIForm_Load (FRMMAIN.BAS:130) does not do it in code.
The port's frame is an ordinary window opening at 1180x760 (CityWindow.axaml), and the children's saved places are only
right in a frame of the size they were saved in. So the frame's normal size, place and maximised state are written to
settings.ini as Frame, beside NotFirstTime, as it closes. They are put back before the city is shown, and the children
are placed once the frame has its final size. A saved place is ignored unless some display's working area shows part of
the frame's top 30 pixels, its title strip, where it is taken hold of to move it: a sliver of its bottom edge on a
display could never be dragged back. When only the place is ignored, a frame saved maximised still opens maximised, on
the primary display.

**Deviation, at the user's request: the automap grows with its window - 2026-10-14, smooth since 2026-10-15.** A GUI
change, not a rule of the game. The original never scales picMaze: every blit is BitBlt at 1:1 and modAdjustMap only
moves it (see "The automap is not scaled" above). The port enlarges it - picMaze and sub2E18's numbers together - when
the form is at least full size both ways (458x493, the size at which the original stops sliding it) and the picture is
bigger than the map. The factor is AutomapLayout.Zoom: the largest that fits both ways, keeping the map's shape, less
four device pixels for the rounding below, and never under 1 - any factor, 1.02x as readily as 3.65x.

It began as whole steps of *device* pixels per map pixel (2x and 3x at 100%, 1.6x and 2.4x at 125%), because a plain
fractional stretch of the map wobbles: the map is the original's 15x15 icons (AutomapPics, out of MG11.DLL) with
one-pixel walls, and stretched by 1.5 half the walls come out one device pixel wide and half two. Smoothing was
rejected too: it blurs the walls into grey. But the first whole step needed a window of about 731x805 at 125%, so the
user's map, in a maximised game, never grew; they asked for smooth scaling instead (2026-10-15).

How it stays sharp at any factor. The icons are bitmaps, so there are no lines to draw again at another size; the port
redraws the level's plotted *pixels* for the device instead, nearest neighbour but laid by the square
(AutomapLayout.DeviceEdges): every square starts on the whole device pixel nearest where the factor puts it, and inside
a square the fourteen pixel columns (and rows) always get the same widths, offset by offset, so every wall, door, icon
and the arrow is drawn the same in every square. A square comes out floor(14 x the device factor) device pixels or one
more; that one more goes to one interior offset, never to the line on the square's edge, so every wall on the map is
one thickness. The bitmap is exactly the device pixels it covers and is shown one to one on a whole device pixel, so
nothing scales it again. The cost is that squares differ by one device pixel (say 22 and 23 at 1.6), which reads as
nothing next to walls that wobble. The numbers round the map are text in a box scaled by the same factor.

It is drawn again only when the size or what is on the map changes, never per frame. A change to the map (a step, a
level paged, a re-plot) draws it at once. A single resize - a double click, a maximise - draws it once the layout has
settled. A drag draws at its first move and then stretches that bitmap (nearest neighbour, briefly uneven) until the
pointer has been still for 150 ms, and draws once more at the size it was left at; the level is not plotted again for
any of it. Moved to a display of another scaling, it is drawn for that one.

Below the original size - every form smaller than full size, and a full size form whose picture is smaller than the
map - the placing is unchanged: the map is its own size at the top left, or slid to put your square in the middle,
exactly as modAdjustMap has it, and never made smaller. It is drawn for the device the same way there, so at 125% its
1.25 device pixels a map pixel are laid by the square too, rather than stretched by Avalonia as before. Everything on
the map works at any factor: the blinking arrow is the same picture drawn twice, the right click finds picMaze by where
the pointer is on the map as drawn (AutomapLayout.ToMap, OnMaze), and the double click (map and form only), the level
bar and the labels are untouched; the level shown is kept across resizes, and the size across levels. Checked by
Mordor.UiCheck (headless) and selfcheck.

How big the window has to be: the map with its numbers is 438x434, and the window round the picture takes about 30
across and 110 down (title bar, border, the level row, the margins), so the map starts to grow once the window is
past about 472x548, at either scaling, and grows with every pixel after that - the shorter way deciding, so a window
2000x800 enlarges it by about 1.58 and one 800x1600 by about 1.75. Mordor.UiCheck drags the grip to 100 past full
size, to 1300x1150, 1900x1500, 2000x800 and 800x1600, maximises and restores, at 100% and at 125%. Every case comes out
at exactly the factor the picture's room gives, with the bitmap the device size DeviceEdges gives, shown one to one,
inside the picture, and every device pixel the map pixel it should be; and each drag drew the map at most twice
before it settled.

The same request asked that resizing keep the level being shown, and it does. One difference from the original goes
with it: there, Form_Resize posts "UPDATE" (FRMAUTOM.BAS:151-156), and with "Auto Change Lev." ticked medDrawCursor
then turns hsbLevel to the level you are standing on (306-313), so resizing the original while looking at another
level takes you back to your own. The port does not post that "UPDATE" on a resize, so the level paged to stays.
Unticking "Auto Change Lev." gives the original the same behaviour.

**Deviation, a port necessity: the frame scrolls to a window past its edge - 2026-09-27.** An MDIForm's ScrollBars
property is True by default, and the frame then shows a scroll bar along an edge whenever a child lies past that edge of
its client area. FRMMAIN.FRM, where frmMain would set it otherwise, is not among the decompiled files, so the VB3 default
is taken. sub0524 (MAINMOD.BAS:487-494) only pulls back a window past the whole display, so a window saved low in a taller
frame (Messages at Top 850 in the 760-tall one) stays there and is scrolled to. The port's MdiHost clipped such a window
out of sight, with no way to reach it. It now shows a bar for either axis when a shown, normal child lies past that edge
(MdiScroll.Bars). The bars move the view only: a child's own Left and Top, which are what is saved, do not change. They
lie over the frame's edges rather than taking room from it, so the area the window sizes are scaled against is the same
with them or without, and each bar's range goes a bar's thickness further to uncover what it covers. Minimised icons
stay in the bottom row, above a horizontal bar. A maximised child fills the area, and while one is up nothing scrolls. A
window being dragged keeps the view where it is until it is let go.

**A service form in a frame smaller than it keeps its size, and the frame scrolls to it - corrected 2026-09-28.** The six
town services drawn to their forms' data (the Guilds, the Bank, the Morgue, the Seer, Confinement, the Store) have a
Fixed Double border and no scrolling of their own: the window's client is the form's client. PlaceService still cut the
window down to the frame, so in a frame smaller than the form (the Store needs 480x316 pixels) its right and bottom
controls were clipped with no way to reach them. A Fixed Double MDI child is not shrunk by a smaller MDI client; the
client's scroll bars (above) reach it. The port now does the same: a form-drawn service keeps the form's size, is placed
no further left or up than the frame's corner, and whatever lies past the frame's right or bottom edge is reached with
the frame's scroll bars (MdiScroll.Bars). The Library and the Guild Log are drawn to their forms too now, and are
placed the same way. The dead screen, the port's own page, scrolls inside its window, so it is still capped to the frame.

**Game keys reach the game before the controls - corrected 2026-09-27.** Every game window's Form_KeyDown takes a key
before its controls and hands it to fn1521, or below ground to the key queue: the character window (FRMCHAR1.BAS:39-53),
Objects & Spells (FRMCHAR3.BAS:310-331), Messages (FRMCHAR4.BAS:17-32), Party (FRMGROUP.BAS:51-66), the automap
(FRMAUTOM.BAS:55-72), the Dungeon (FRMDUNM.BAS:201-220) and the City (FRMCTYM.BAS:35-64). All but the City clear a key
fn1521 used; the City's takes its Alt letters and hands every other key to fn1521 without ever clearing KeyCode
(FRMCTYM.BAS:57-61). Only a text box and a combo
box keep their keys, and in Objects & Spells and the automap a scroll bar too. In the port a clicked control took the key
first, so after a click on the character window's Guild tab, Left and Right changed tabs instead of turning the party.
Both frames now preview every key for the game on its way down, except in a text box, combo box or scroll bar and except
Tab; a key the game does not use goes on to the control. The town's service windows (FRMCTYS, FRMCITYB, FRMCITYC,
FRMCTYG, FRMMORG, FRMSEEK) hand the key to fn1521 without clearing it, so both see it, and keep their own handling.

**fn15DF's keys work in a service's text box and combo box - corrected 2026-09-27.** The General Store, the Companion
store, the Bank, the Morgue and the Guilds hand a key to fn15DF, not fn1521, while their active control is a ComboBox or
a TextBox, and clear it when fn15DF used it (FRMCTYS.BAS:407-425, FRMCITYC.BAS:380-398, FRMCITYB.BAS:86-103,
FRMCTYG.BAS:598-615, FRMMORG.BAS:198-215). The test on Me.ActiveControl only means anything with the form's KeyPreview
on, so the form sees the key before the box. fn15DF (MAINMOD2.BAS:4175-4241) takes Ctrl with L, B, M, S, R, C or G for a
character-window tab and I, P or O for an Objects & Spells page, and Alt with a digit for the Party window's lblCall, in
a party and for a slot that exists; its Alt+Tab and Ctrl+Escape are Windows' own. The port's city gave every key in such
a box to the box, so none of these did anything there. They are now taken first, and the box keeps the focus (unless a
minimised character window comes back and takes the activation - see Ctrl+L above, 2026-10-25). The Seer
is not among them: its Form_KeyDown hands every key to fn1521 (FRMSEEK.BAS:374-379).

**The help closes when the game ends - corrected 2026-09-27.** mdrEndProgram calls WinHelp with HELP_QUIT
(MAINMOD.BAS:938; extfn012C, mc01E6 = 2), which closes the help. In the port a help window left open kept the process
running after the last game window had gone. Every open help window now closes when the main window closes, and on a
session end once the game's own question has been answered Yes (FRMMAIN.BAS:176-192), where mdrEndProgram runs. The port
had closed the help as the session-end request arrived, so a No, or "Cannot exit while monsters are in the area!", left
the game running without its help; a help window now holds off a session end's close until that answer.

**The toolbar is current as soon as the town is back - corrected 2026-09-27.** Unload frmDungeonMain and frmCityMain.Show
are one step in the original (MAINMOD2.BAS:3957-3958). The port put the town back, then read the options, placed the
windows and started the music before it refreshed, so the toolbar's gold and experience lines were stale for about a
second. The character window, Party and toolbar now refresh the moment the town view is back; the refresh after the
rest still follows.

**The Automap on its form, and one Automap for the town and the dungeon - corrected 2026-09-27.**
- **Placing the map.** modAdjustMap tests the form's own size against the table's 458x493, each axis on its own
  (FRMAUTOM.BAS:461, 478). The port read the child window's Bounds from inside its arrange, when they are still the
  old size - 0x0 the first time - so every form was "narrow" and "shallow" with a room of one pixel, and the square you
  stood on landed at the picture's top left with the map beyond it cut off (the user's 594x690 window, 475x553 before
  display scaling, standing on 9,11). It now reads the size the child was given. At or above full size the whole map
  sits at its natural size from the left and top (473-476, 490-493); below it, per axis, it is centred on your square,
  `(X - 1) * 14 + 15 / 4` and `(Height - Y) * 14 + 15 / 4` (465, 482), or the level's middle while lost (467, 484).
  The map is never scaled. The picture's room no longer counts the level row's width either: a star column had been
  widened by it and put the middle ten pixels off.
- **The level row.** On a form shorter than full height picPicture goes to Top 0 and fills the form (479-480), over
  hsbLevel, lblLevel and lblInfo; it sits below them only at full height (492). Narrower than full width it goes to
  Left 0 and takes the form's width (462-463). The port always showed the row.
- **The numbers.** sub2E18 prints the columns only at full width and the rows only at full height (508, 515); a picture
  grown to fill the other axis covers what was printed. The port always showed both.
- **One frmAutoMap.** The town and the dungeon share one form: nothing on the way down or up unloads it (only sub16E0,
  MAINMOD2.BAS:5970). frmDungeonMain's Form_Load puts a loaded map back to a normal window and says "UPDATE"
  (FRMDUNM.BAS:297-302); frmCityMain's says "UPDATE" and minimises it (FRMCTYM.BAS:149-156). So it keeps its level and
  what is drawn on it, and in town it says "You are in the city." with the arrow gone. The port built a town map of its
  own, read once, which never showed a trip's exploring; the one map now passes between the two frames, and a map
  shut in one stays shut in the other. Form_Load's level choice - your level below, level 1 in town (FRMAUTOM.BAS:94-97)
  - applies only when it is loaded, at sub14E0 (MAINMOD2.BAS:3164) or on F8 with none open. A character run below
  ground has the map loaded by the dungeon, on their level.
- **F8** is `frmAutoMap.Show` and `WindowState = False` (MAINMOD2.BAS:3560-3563): an open map is restored and brought
  up. The port had closed it on a second press.
- **A small map in town** centres on Chars(0).X and .Y, which still hold the square the leader walked out from.
- **The map is moved only when modAdjustMap runs.** Its callers are Form_Resize (FRMAUTOM.BAS:164) and medDrawCursor's
  branch that draws the arrow - below ground, neither M59F4(0) nor M59F4(1) up, on the level shown (320-322). The
  other-level branch (328-331), the lost one (333-339) and town (340-342) leave picMaze where it was. The port centred
  it again on every redraw, so a small map paged to another level jumped back to your square.
- **The double-click only resizes.** picMaze_DblClick sets the size and refreshes (585-598); only the right button
  re-plots (601). The port re-plotted on a double-click as well, which turned the grey-edged squares solid.
  `picPicture_Click` (604-606) makes a single click on the frame round picMaze the same toggle; the port lacked it.
  A right click there does nothing: only picMaze has a MouseDown (600-602).
- **The level the map opens on** is Form_Load's alone: your level unless M59F4(0) is up, else 1 (94-98). The port also
  asked "Auto Change Lev." and both lost flags.
- **Chars(0).** FRMAUTOM reads Chars(m0078), m0078 = 0 (24), for the flags and for M5914: the leader, not the character
  whose commands are being entered - mdrmulSwitch only moves Me (MULTIMOD.BAS:618). The dungeon had handed the map the
  selected character's lost flags and Auto Change Lev.
- **The dead get the dungeon's map.** sub14E0 shows frmDungeonMain, not frmCityMain, for a dead character
  (MAINMOD2.BAS:3170-3180), and its Form_Load restores a loaded map (FRMDUNM.BAS:297-299). The map opens on the body's
  level (Form_Load reads Chars(0).DungeonLevel) with the arrow on its square. The port had given them a town map,
  minimised. After a raise at level 0, sub14CC shows frmCityMain (MAINMOD2.BAS:3150-3152), whose Form_Load says
  "UPDATE" and minimises the map (FRMCTYM.BAS:149-156); the port had left the map as the dead screen had it.
  The map is the original's, and since 2026-10-19 the screen around it is a window of its own with the town hidden
  while it is up (`ShowDead`; see "The dead screen stands where frmDungeonMain does" below).
- **q loads no city.** "Exit to Main Menu" goes to sub16E0 and frmMenu with no frmCityMain (MAINMOD2.BAS:6316-6331), so
  the map is not minimised on the way out and its placement is saved as it stood. The port had minimised it first.

**Objects & Spells, the chest, the dead screen and the message lines - corrected 2026-09-27.**
- **Equip and Drop move on.** After Equip the selection goes to the next row, whatever sub0F74 did: `sub0F74 Me,
  m001A` then `If m001A < mc0122 - 1 Then lblItem_Click m001A + 1` (FRMCHAR3.BAS:212-216). A Drop that goes through
  does the same (241-243). Both happen in the dungeon, and Equip does in the city.
- **Drop throws away the slot you picked.** It removes the slot on screen, as `mdrcharRemoveItem Me, (m001A)` does
  (FRMCHAR3.BAS:240). The port had counted carried items instead, with Hands and empty slots left out, so it dropped
  the item below the one you had confirmed.
- **The chest's labels.** frmDungeonMain's form data in MORDOR.EXE (RCDATA 22) gives lblTrap Alignment 1 (Right
  Justify) and no font of its own, so the form's MS Sans Serif 8.25 bold. It gives lblDisarm Courier New 7.5 bold, also
  Right Justify. The disarm gauge is right-aligned, as the user thought. lblTrap starts 195 twips left of picTrap and VB
  clips a child to its container, so the panel clips too. The port had lblTrap in Courier New, centred and unclipped,
  and "Magically Locked" ran out of the box. The selfcheck reads these properties out of MORDOR.EXE.
- **A chest's item shows at once.** monOpenChest paints the slot with `mdrcharShowItem` as the item lands, whoever takes
  it (MONSMOD.BAS:3849). When alone, it also repaints the buffers (3834-3836). The port's pack list was rebuilt only by
  whatever redrew it next. The list now catches up whenever the party window repaints and the pack has changed, so a
  quicksand loss or a thief is caught too.
- **Which chest items put themselves on.** The slot copies the item's curse flag whatever it is (MONSMOD.BAS:3830). An
  unequipped chest item that is flagged cursed is therefore the original's behaviour. Only `Items.Cursed = gc1DD0`, which
  is 2 (MAIN.TXT:694), goes through sub0F74 (3851). The data has 38 items with a 1, the Bone Dagger's kind, and five
  with a 2: Ball and Chain, Tim's Lead Weights, Dalyn's Evil Laugh, Derf's Toe Lint and Curse of Morash. The port put on
  anything cursed. It now puts on only the 2s (`Loot.AttachesItself`).
- **The dead screen.** For a lone character, sub0930 turns the fight clock off, hides picTrap, captions and shows
  lblDead, and shows cmdWait. It then writes lblSquareInfo and hides picMonsters (MAINMOD2.BAS:3039-3057). It clears
  picMaze2 first (3024), and mdrmazShowMaze draws nothing while the leader is dead (MAZEMOD.BAS:773-776). Rounds check
  the clock and stop (MONSMOD.BAS:294, 374, 506). The port instead put the fight panel up to hold the death state and
  left the clock running. The fight went on over the body ("You Killed One!", a monster's spell), the chest and
  "Magically Locked" stayed up, and Wait for Rescue lay across the monster picture. A death in a fight now also reaches
  this screen; that path had had a half-built copy of it.
- **lblCurrentGroup with no order.** It is Chr$(149) (MONSMOD.BAS:4217), which is not a character in Windows 3.1 ANSI.
  MS Sans Serif draws its block for it, and game_files/dmainw1.png shows a narrow upright bar. The port had drawn a
  bullet, which read as list styling.
- **The Dungeon caption.** mdrmulSwitch writes "Dungeon (Name)" for any party size (MULTIMOD.BAS:622-623). sub1536
  writes it only for a party (686-690), and "Dungeon" is the form's own caption. A character loaded below ground
  therefore keeps "Dungeon (Name)" even alone: sub14E0 shows frmParty first (MAINMOD2.BAS:3163), and its Form_Load runs
  mdrmulSwitch (FRMGROUP.BAS:91). The same is true after the party list's UPDATE (383-389) or a drag between names
  (247-249). Walking in from town, or dying there alone, leaves "Dungeon". The port had worked the caption out from the
  party size alone.
- **Switching keeps the chest.** mdrmulSwitch redraws the chest from the Fight record (MULTIMOD.BAS:625-627). For a
  character with no hits monShowChest leaves at once (MONSMOD.BAS:4090) and erases nothing, so the chest last drawn
  stays. The port read "no fight" as "no chest" and painted it out on every switch after a won fight.
- **Shift with an arrow.** Left, Right and Down with Shift turn and then take a full step, `fn1521(mc05EC, False)`
  (MAINMOD2.BAS:3903-3942). Paralysis still stops both.
- **Message lines the original does not have.** FRMCHAR3 has no message label; its labels are lblItem, lblItemStatus,
  lblGuild, lblName, lblLevel, lblPoints, lblCompName, lblCharacterHit and lblCall. The port's line under Objects &
  Spells is gone. lblSquareInfo, under the view, is written only by sub0835 and the square routine
  (MAZEMOD.BAS:1545-1863), the drowning and sinking lines (FRMDUNM.BAS:507-510) and the death code
  (MAINMOD2.BAS:3052-3054). sub085B lines are no longer echoed there: look-ahead, casts, refusals, fight messages, the
  give and the swap. Several lines had gone only to that label and never to Messages. They now go to Messages, word for
  word: the pool lines (MAINMOD2.BAS:3638-3651), "... is now party leader!" (FRMGROUP.BAS:243), "... is no longer
  carrying ..." (MAINMOD2.BAS:3694, 3698), the identify line out of a fight (FRMDUNM.BAS:471-494), and Equip's and
  Drop's refusals. An item's cast and its effects are separate Messages lines, where they had been run together.

**A lone death mid-round, the caption after a break-up, the chest's curse, and the city's Drop - corrected 2026-09-27.**
- **A lone character killed by a blow is seen to die.** The engine takes the hits off before it yields the blow's caption,
  as sub0E85 does before lblMonsterHit (MONSMOD.BAS:3030-3035). PlayRound had stopped on "alone and wiped" before
  drawing each line, so that caption and its sounds were lost. So were "You were killed by a X." (3036-3040) and the
  death behind it. The stop is gone. The engine still ends the groups once a lone character is down
  (MULTIMOD.BAS:364), and the round ends on IsOver.
- **EndCombat runs all of sub0930 for a lone death.** sub0A64 calls it for the character at nought (MULTIMOD.BAS:362-363).
  So the companions and a carried body go (MAINMOD2.BAS:3026-3030), and sub048C runs (3033). The port had shown only
  the dead screen. A companion's mishap is mdrcharSetDead alone and had left both undone.
- **After a lone mishap death the monsters still draw - corrected 2026-10-19.** After a lone character dies of a
  companion's mishap, the original's sub0A64 still runs once the characters are done (MULTIMOD.BAS:556-557): it asks
  only that monsters stand. This entry had said its first group may attack the body. It cannot: the group draws its
  target (MULTIMOD.BAS:315), but sub0E85 leaves on its first line for anyone with a DeathState (MONSMOD.BAS:2598),
  which mdrcharSetDead has just set (MAINMOD2.BAS:4577). Then sub0930 runs for the body and the Exit For ends the
  groups (MULTIMOD.BAS:362-364). So the turn is one draw and no blow. The port's round stopped on IsOver and made
  no draw; it now goes on to the monsters while any stand, and GroupAttacks leaves at once for the dead, as sub0E85 does.
- **A chest trap that kills a lone character mid-round loses no room.** OpenChest writes the room with the trap gone
  first (fn0F88, MONSMOD.BAS:3514-3515), with the fight's groups as they stand. Nothing changes them before the death.
  mdrcharSetDead's own fn0F88 (MAINMOD2.BAS:4560) would write the same records. Closing the window writes the room
  again. EndCombat not running on that path loses nothing.
- **The caption after sub0908.** Whenever sub0908 drops anyone, it ends in sub1536 gv1A84 (MULTIMOD.BAS:93-95). The
  UPDATE before that leaves at once for a list of one (FRMGROUP.BAS:319-321). So a party split to one by a random
  teleporter, a chute or the chest's Teleporter trap reads plain "Dungeon" (MULTIMOD.BAS:686-690).
- **The caption on activating the character windows.** frmCharStat1 and frmCharStat3 run "If gv19C2(..).M5E4F <> Me Then
  sub1536 Me" when they become the active form (FRMCHAR1.BAS:33-37, FRMCHAR3.BAS:304-308). Only sub1536 writes M5E4F
  (MULTIMOD.BAS:668-683). The prune moves Me down past a removed slot (FRMGROUP.BAS:339-341) but not M5E4F. It re-runs
  sub1536 only when the removed one was gv1A82 (336-338, 360-361). So after a party is pruned to one, a click that
  activates either window can put the caption back to "Dungeon". The port now keeps that slot (`_shownSlot`). It
  sets it at sub1536's own sites: a double click on a name, Leave, sub0908 and the prune. The port has no gv1A82, so
  the prune reads M5E4F in its place; the two differ only after a drag between names. A click counts as
  activating the window, and since 2026-10-25 so do a restore from an icon and an Info's SetFocus (see "The
  character window is restored where mdrcharSetInfo restores it").
- **A chest item that attaches itself says so either way.** MONSMOD.BAS:3850-3858 calls sub0F74 and then, whatever
  sub0F74 did, says the attach line and plays sub086D 61 and sub09DE 63. Those are EXTING.WAV and YEECH.WAV
  (MODULE1.BAS:369, 371). sub0F74's own refusals are said too (MAINMOD2.BAS:1786, 1816, 1818). A thing that cannot be
  worn at all is refused without a word (1729-1731). The port had said nothing when sub0F74 refused. It had also played
  BLACKOUT, which is SoundFiles(7) (MODULE1.BAS:315). The selfcheck derives the two sounds from the source.
- **Objects & Spells in the city has Drop, and heeds paralysis.** frmCharStat3 is one form, and its form data in
  MORDOR.EXE holds cmdCommand(0..3): Equip, Use, Drop, Info. All four have the same properties, none hidden. Drop's
  case asks nothing about DungeonLevel (FRMCHAR3.BAS:227-246); only Use does (222-226). The city's Drop is now the
  dungeon's: guild items refused, "Drop the X?", a worn cursed item refused with a Beep, then the slot emptied and the
  selection moved on. All of cmdCommand_Click is inside "If Ailments(mc0116) = False" (FRMCHAR3.BAS:203). So the city's
  Equip, Use, Drop and Info now do nothing while paralysed, and the selection stays put. frmCityMain's Form_Load clears
  paralysis for everyone there (FRMCTYM.BAS:115), so this rarely matters.
- **Drop's cursed refusal is a Beep** (FRMCHAR3.BAS:236), the system sound. The dungeon's had played OUCH.WAV. Its
  "Drop the X?" names the item with its alignment and charges, as itemNameFor is called there (234).
- **Citations corrected.** Chr$(149) is MONSMOD.BAS:4217. The chest slot's curse copy is 3830 and the buffer loop is
  3834-3836. fn1521's Up arrow is MAINMOD2.BAS:3770-3902, and its wall test is 3772. Drop is FRMCHAR3.BAS:227-246. The
  dead screen's lblSquareInfo line is MAINMOD2.BAS:3052, and its Refresh is 3057. sub0908's hand-over to the leader is
  MULTIMOD.BAS:78-84. The city's Use refusal is FRMCHAR3.BAS:225.

**Final review fixes - corrected 2026-09-27.**
- CTRL-letters typed in a service's box only change the tab. fn15DF assigns `tabStatTab`/`tabItemTab` and nothing else
  (MAINMOD2.BAS:4187-4222), so the character window and Objects & Spells are neither restored nor raised over the
  service. The port had brought them forward, which covered the Store's search box. (Since 2026-10-25: the
  assignment fires the tab's Click, so Ctrl+L, R, C and G restore a minimised or maximised character window -
  measured, see "Settled: Ctrl+L" above; a normal one is still neither restored nor raised.)
- The Switch To button and ALT-s run sub1536 when the shown slot (gv1A82) differs from the character in command
  (FRMGROUP.BAS:35-36, MAINMOD2.BAS:3416-3418), so after a prune they put the caption back to plain "Dungeon".
- A cursed, worn item's drop refusal quotes the row's own caption: "You can't drop item 3. Bone Dagger!  It's
  cursed!" (FRMCHAR3.BAS:237; the caption is built at MAINMOD2.BAS:5434-5436). The port had given the number alone.
- Port necessity: the frame's scroll range holds still while a window's resize grip is held, as it already did for a
  title-bar drag, so shrinking the window that sets the range cannot move the others under the pointer.

**The port's own: a mod system, off by default - 2026-09-27.** docs/MODDING.md describes it. Mods live in `mods\`
folders, are listed on a Mods page reached from a "Mods..." button on the main menu (since 2026-10-04, the one control
added to frmMenu: the column's 250 x 27 at 175,293, a row above HelpLesson and Options at their pitch, in the gap
under Delete Character; until then it was the one control added to the Options form of bm3.png, which is the
original's again) and, so that a mod's toggles can be changed in a game, from a "Mods..." button on the Control
Panel beside Game Options (the one control added to the form of bm0.png), and each must be switched on there; none is by default. With no mod on, every hook gives the
original's answer: the Store's alignment choice locks the store (above), ALT-p pools the on-hand gold, the toolbar has
no tooltips, and Objects & Spells and the Party Information window are as they were. `tools/check.sh` asserts those
defaults (the `mods` probe) along with everything else, with no mod loaded. Two things change even with none on, and
neither changes play:
- A save made while mods are running records them ("Mods": ["QoL@1.0.0"]). With none running the key is not written,
  so a save is byte for byte what it was. Loading a character whose save names mods that are not running now says so
  once, in an OK box, and then plays.
- The Bank's Party Pool & Deposit arithmetic (FRMCITYB.BAS:48-59) moved from the Bank's button into Party, so that the
  Quality of Life mod's ALT-p uses the same rule. The Bank's result is unchanged.

The Quality of Life mod ships with the port and is off until enabled. Its five toggles are the user's requests: the
unlocked Store choice (commit 164befa's behaviour), ALT-p in town doing the Bank's Party Pool & Deposit, toolbar
tooltips named as the help's Toolbar page names each button with the key it gives ("AutoMap (F8)"), a Sort button in
Objects & Spells, and a "Next" column in the Party Information window with the toolbar's own "more experience for
level" figure. Sort keeps Hands in slot 1 and moves each slot whole, with buffers following their items the way a
dragged swap moves them (FRMCHAR3.BAS:571-577). It is refused while paralysed, as Equip, Use and Drop are
(FRMCHAR3.BAS:203), but not in a fight, because Swap Items has no such test (MAINMOD2.BAS:3415-3439).

**Item tags, Ask to Join, the portrait strip, the Store's list and the gold line - corrected 2026-09-27.**
- A fully known aligned item is named with gv077E$'s tag: "Iron Dagger (g)", not "Iron Dagger Good". itemNameFor
  appends " " + gv077E$(alignment) for an alignment above nought (MAINMOD2.BAS:2571-2573), and gv077E$ is "(u)",
  "(g)", "(n)", "(e)" (MODULE1.BAS:552-555). The item's Misc page (5047), the Store's "(g)3 Value" and "1(g) Cost"
  and the Confinement's captions already used the tags; Loot.NameFor, which every list and message goes through,
  had used the words.
- Ask to Join no longer refuses in silence. fn1521 makes no square test: it loads the character and puts them on
  Chars(m00A6)'s square (MAINMOD2.BAS:3507-3509); frmInteract's list is by level and area (FRMINTER.BAS:201). The
  port's Party.Add asked for the same square against the party's stored position, which follows the window only on
  an area change, so a join inside an area failed and said nothing. The position is brought up to date first, the
  newcomer takes the leader's place, and any refusal left is said. The town does the same.
- monShowMonsters' portrait strip (MONSMOD.BAS:4351-4396): one 60x60 portrait per group with GroupCount > 0, side by
  side at centre - 30 * count + (n - 1) * 60, y 7, copied straight off the sheet. The count loses one, not below
  one, when the lead slot is Asmodeus (fn0C3B, MONSMOD.BAS:747-756: monster id 277), who is never drawn; an
  invisible monster's place is painted blank unless the character in command has See Invisible (4381-4393). It is
  redrawn with the groups: an encounter shown or engaged, each RefreshCombat (a group dying), and the character
  switch (mdrmulSwitch, MULTIMOD.BAS:628-630). The port had shown the lead group's portrait alone, in a frame of
  its own.
- The town's Objects & Spells leaves the equipped mark's two columns on an empty row too ("  7."), as lblItemStatus
  is its own label (MAINMOD2.BAS:5429-5433) and the dungeon's list already did.
- The Store's list is fixed labels that clip: no sideways scroll bar over the last row, and no ellipsis. "Crystal
  of Healing [12" is cut at the column's edge.
- Tome of Lies (item 214) has Cursed = 0 in MDATA3, with alignment flags 14 (Good, Neutral, Evil), floor 1. Selling
  one shelves it (FRMCTYS.BAS:267 tests only Items().Cursed), and the restock can add one, so evil copies on the
  shelf are the original's. No port path shelves an item whose Cursed is set; selfcheck asserts it for the opening
  shelf and two hundred restocks.
- monGetGold's line - "Your share is N Gold!" or "...and you recovered M Gold!" - is lblCharacterHit's (MONSMOD.BAS:2158,
  2174), held by sub07E2 .5 (2175) and then hidden (2176). It has no sub085B, so it is no longer a Messages line. It follows
  sub0C53's painting-out of the groups (780, 784), the chest's half second on picTrap (3650-3651, 3660), or the
  join that empties the room. Of the four wordings only those two can print: both "didn't find any" branches sit
  inside `If l039C > 0` and need l039C to be nought (2159-2173), and with nothing found or recovered the caption is
  blank. **The blank is written - corrected 2026-10-19:** the label is made visible (2158) and given that blank
  caption (2174) outside the If, so whatever it held - "You Killed One!" from the last blow - is wiped for the half
  second. The port had left the last caption up. `PayGold` now leaves "" in GoldLine for that case (null only when
  monGetGold did not run) and the window writes it; an empty label shows nothing, so no panel is put up for it.
- The General Store is told "UPDATE" (lblCall_Change, FRMCTYS.BAS:479-502: the Identify, Combine and Uncurse boxes
  emptied, their buttons greyed, the rows redrawn) after a dragged swap (FRMCHAR3.BAS:583), after a mod command
  that moves the pack (Quality of Life's Sort, a run of such swaps), and on a character switch in town
  (MULTIMOD.BAS:633). The boxes hold slot numbers; Sort had left them naming slots whose items had moved.
- The typed Swap Items ('s') exchanges the two Pack records and remaps no buffer (MAINMOD2.BAS:3432-3442); only the
  dragged swap moves the buffers with their items (FRMCHAR3.BAS:571-577). The port had remapped on both.
- Quality of Life's town ALT-p (Party Pool & Deposit) looks at the Wealthiest record whether or not the Bank is
  open, as the Bank's button ends in sub2884 (FRMCITYB.BAS:62, 284).

**Verification follow-ups: the key queue through sub07E2, the chest's order, the Store's UPDATEs - corrected 2026-09-27.**
- Keys wait through monGetGold's and monOpenChest's half seconds. Form_KeyDown hands a key to sub0456, which only
  queues it (FRMDUNM.BAS:201-215, MAINMOD.BAS:89-101); the queue is run by sub0695 from sub1738's main loop
  (MAINMOD2.BAS:6310-6316, MAINMOD.BAS:1063-1168), never from inside a routine waiting in sub07E2 (MAINMOD.BAS:1989-1999).
  So a key pressed while monGetGold holds its caption (MONSMOD.BAS:2175) - after a won fight with no chest (783-784)
  or a join that empties the room - or while monOpenChest waits (picTrap's 3650, then monGetGold's) is done when the
  wait is over, in order. The port took keys at once, so a step out of the area straight after a win went through
  HideMonsters and wiped the gold caption the moment it was up. The dungeon now holds its keys (and the toolbar
  buttons, which only queue a key, FRMDUNM.BAS:76-109) for those waits and replays them after; the window still draws
  and the mouse still works, as DoEvents allows. monGetGold's sub07E2 .5 is outside its If, so the wait is there
  even when the caption is blank. A key pressed while a won round is still being drawn first finishes that round's
  sub0C53, as the original's round runs to its end before the queue is looked at.
- The chest's treasure comes after both half seconds. monOpenChest shows the trap's caption, waits .5 (3650), takes
  picTrap down, calls monGetGold (3660), which waits .5 with its caption (2175), and only then goes through the
  treasure (3661-3866). The port had given the items and written their lines at the moment of opening. The Fight
  record's lead monster, TreasureClass, ChestLock and the leader's level are taken at the opening, the record is
  emptied and written back then as before (3868-3869), and the loop runs a second later - or at once if the dungeon
  is left or the area changed first (FinishWonFight), so no leaving can lose it.
- The General Store is told "UPDATE" after an item dragged onto a party member, whatever sub14B5 made of it
  (FRMGROUP.BAS:205-206), and after every item drop on the pack, including onto the same slot or the Hands, which
  swap nothing (FRMCHAR3.BAS:562-563, 583). The port had sent neither.
- The portrait strip's top is y 7 (MONSMOD.BAS:4383, 4385), drawn at 6 on the port's canvas: picMonsters is drawn
  there with the original's co-ordinates less one throughout (sub0B9B's 3,8 is StateIcon's 2,7, monShowChest's y 8
  is ChestIcon's 7, ScaleWidth / 2 is 182), and in game_files/dmainw1.png the portraits' top is one row above the
  skull's, as in the port. Measured against that screenshot the original's client area starts two pixels inside a
  sunken edge where the port's panel has a one-pixel border, so the whole panel's contents sit about two pixels
  higher than the original's relative to its outline; that is a property of the panel, not of the strip, and is left.
- The Quality of Life Store option (arrow keys move the list while Select Alignment is up) is read afresh when a mod
  setting changes and at every change of row, not once when the Store opens; with it off the list takes no keys
  (FRMCTYS.BAS:407-424) and gives the keyboard back to the form. The press/release pairing that keeps a click from
  running elaItem_Click twice is set and cleared by the left button only, and cleared when the pointer capture leaves
  the list.

**The key queue after a hold - corrected 2026-09-27.** Held commands are done one at a time, as sub0695 takes them
from sub0456's queue (MAINMOD.BAS:89-101). A held key that puts up a box, such as q's "Exit to Main Menu?", now waits
for the answer before the next held key runs, as the original's MsgBox blocks the queue. The buffer buttons and the
Spell, Take and Map buttons join the same queue, because cmdBuffer_Click and cmdAction_Click only hand their key to
sub0456 (FRMDUNM.BAS:76-122). After a chest the keys are held until its treasure has been given, since the queue is
not looked at until monOpenChest returns (MONSMOD.BAS:3650, 3660-3866).

**Review fixes - corrected 2026-09-27.**
- A character switch in town tells an open Guild "UPDATE" too (MULTIMOD.BAS:636). lblCall_Change (FRMCTYG.BAS:671-686)
  puts the page back on the new character's current guild, redraws it (sub294F, sub2974, sub2962) and greys Make
  Level for want of experience (fn12A1) or gold on hand and banked (fn12B2), as Form_Load does. The port had told
  the Store, Confinement, the Morgue and the Bank, and left the Guild showing the character before. The five are now
  told in the original's order (633-637); the Seer (frmSeek) is not among them and is left alone.
- The trap readout on a chest from a new encounter is told to the mods even when it reads the same as the last
  chest's (the Combat Log had dropped it as a repeat). Nothing the player sees changes.
- The fn13ED citation is MAINMOD2.BAS:2710-2714 (it had been 2709-2713).
- A stat item's raise is looked at for the Hall of Records whether or not it met the ceiling (race maximum plus
  five): mdrhalCheckRecord (MAINMOD2.BAS:6219) comes after the If that clamps or announces (6214-6218), and only
  the announcement is in its Else. The port had checked the record only when the raise did not clamp, so a potion
  that took a stat to the ceiling could not set the Strength (or other stat) record. A reversed item's lowering
  (6205-6211) checks no record, and still does not.

**Second review fixes: the party list's UPDATE in a fight, the morgue's copies, the Guild's redraw - corrected 2026-09-27.**
- **The party list's UPDATE after the characters' turns.** mdrmulInitiateCombatRound notes anyone dead once every
  character and companion has acted and sends frmParty "UPDATE" before the monsters go (MULTIMOD.BAS:542-553).
  A companion's mishap is mdrcharSetDead alone (MONSMOD.BAS:901-1305) with no sub0930 after it, so this UPDATE is
  what unloads those bodies (modUpdateCharacterList, FRMGROUP.BAS:321-346: everyone at nought while more than one
  is left). The port had no such step: bodies from the characters' own turns stayed on the list until the fight
  ended, and a party turned to stone by its own companions ended the fight with several bodies listed and no
  dead screen, since EndCombat ran sub0930 only for a party of one. Now the round sends the UPDATE (the death
  counted and logged first), and a wiped party with more than one still listed is pruned to the last, who gets
  sub0930 and the dead screen, as the next blow at them does in the original (MULTIMOD.BAS:362-363).
- **The morgue works on the party's own copy.** Hire Rescuers and Raise Character list the characters on disk
  (MorgueList) and write the change to the file (FRMMORG.BAS:161, 172; sub0A7C's Put, MAINMOD2.BAS:5994). In the
  original nobody they can pick is also loaded in Chars(): a party member at nought is unloaded by the UPDATE
  above, and a living one stands with the party in town. In the port a party member could be picked, and the
  party's save that follows wrote the stale party copy over the file, so the gold was spent and the body stayed
  down. The pick is now resolved to the party's copy when there is one, and that copy is changed and saved.
- **The Guild's UPDATE redraws in place.** lblCall_Change (FRMCTYG.BAS:671-686) redraws the form's controls where
  it stands; the port had reopened the service window on every switch, placing it again and scrolling it to the
  top. It now rebuilds the page in the same window at the scroll it had.
- **The switch's UPDATEs follow the leader.** The test in mdrmulSwitch (MULTIMOD.BAS:622) is on Chars(m0022), and
  m0022 is 0: the leader, not the one switched to. A leader below ground or dead takes the dungeon branch and no
  service is told anything; under a living leader in town every switch tells them, a dead member's included. The
  port had told them on every switch.
- The citations for Use (sub15CD) in the item table were in the other numbering: sub15CD is MAINMOD2.BAS:6081-6240,
  mdrcharSubtractItemUse at 6145 comes before the alignment test at 6147-6152, the reversal of positive modifiers
  is 6205-6211, and the rows for the spell's alignment test, the worn-first kinds, the two charge messages, the
  special types and the item used up (mdrcharSubtractItemUse, 5695-5701) now cite the named source's lines.

**The dead screen's Wait for Rescue, and the service windows moved by their title bars - corrected 2026-09-27.**
- **Wait for Rescue, not Hire Rescuers.** A character loaded dead is shown frmDungeonMain and sub0930's screen
  (sub14E0, MAINMOD2.BAS:3170-3182): lblDead, and cmdWait captioned "&Wait for Rescue" while the body is below
  ground or "&Visit the Morgue" at level 0 (3045-3048), and hidden in the rock (3050-3055). The port's city-hosted
  dead screen (ShowDead) had offered the morgue counter's Hire
  Rescuers below ground instead - a port-only button with a FIND CHARACTER list of everyone else, the living
  included. Hire Rescuers is FRMMORG.BAS:104-176, a service for a living character at the counter, and stays there.
  The dead screen's button below ground is now cmdWait_Click (FRMDUNM.BAS:144-196): disabled while the days are
  drawn, a Beep and "You have been found after N day(s)!" in a "Wait for Rescue" box, the body carried to town
  (DungeonLevel 0, M59BB -1) and aged by the days with nothing said, the store and pens turned over for character 0,
  and then sub0930 again (194) - now at level 0, so "&Visit the Morgue", any raise another character cast, and the
  morgue's own box. The rules are Rescue's (DaysUntilFound, EasyToFind, CarryHome, TurnOverTown), which the
  dungeon's own dead screen now calls too, so the two cannot drift apart. The rock is unchanged: the label, and no
  button.
- **Every service window moves.** frmDungeonMain and the six town services are ordinary MDI children with title bars;
  BorderStyle 3 (Fixed Double) fixes their size, not their place. The port had held the dead screen still, on the
  grounds that dragging it would uncover a town the dead are never shown - but frmDungeonMain can be dragged, and
  since every town picture turns a dead character back to that screen, a dead character could move no window of the
  town at all. All of them now move by the title bar and keep their fixed size. The dead screen writes no placement
  of its own, as before; while it stays up (a wait, a refused raise) it keeps the place it was dragged to.
- **The service windows stay above the town (a GUI choice, 2026-10-21).** A click on The City - a picture for another
  service - raised it over a service already up, and the Store's title bar stayed covered until it was clicked. A
  service window is now kept above The City (`MdiChild.StaysAbove`), so bringing the town forward leaves every open
  service over it. Mordor.UiCheck checks it.

**Save and rescue fixes - corrected 2026-09-27.**
- A pack slot's identification level is kept across a save and load. The save wrote both `Known` and an older
  `Identified` flag, and reading `Identified` back turned every Partial or Mostly slot into Barely. `Identified` is no
  longer written, and is only read from a save old enough to have nothing else.
- Waiting for rescue leaves the body's X and Y as they were: cmdWait_Click sets only DungeonLevel and M59BB
  (FRMDUNM.BAS:187-188). The port had also zeroed the square.

**Death clears the ailments on every path the original does, and on arriving dead - corrected 2026-09-27.**
Reported: a poisoned character died, was carried to town and raised at the Morgue, and came back still poisoned.
- **One routine.** `sub0930` (MAINMOD2.BAS:3016-3131) opens the same way every time it runs, dead or not, alone or in
  a party: the companions let go and the carry put down (3026-3030), then `sub048C` (3033, before the party's UPDATE
  exit at 3035). `sub048C` (4359-4373) undoes paralysis through sub0D1D mode 1 (4361), zeroes Ailments 0 to 6 -
  poison, disease, paralysis, Levitating, Invisible, See Invisible, Protected (the constants at MAINMOD2.BAS:51-65,
  the gv07E2$ names) - and the spell resistances, and recomputes. Stone is not one of the seven: it is DeathState 2,
  what the body died of, and stays. That head is now `Character.Sub0930Head` (sub048C alone is `EndEveryAilment`),
  and every port path that stands for a sub0930 calls it: the window's `Kill` (the tick's poison, disease, drowning,
  quicksand and old age, a lone fall, the rock, a fall from the sky), its `Sub0930` (a monster's
  death in a party, the lone wiped fight, fn1521's ending) and the monster attack's own sub0930.
- **The monster attack's sub0930 is where the original has it.** `If Chars(l008E).Hits <= False Then sub0930`
  (MULTIMOD.BAS:362-363) runs for whoever the group picked at nought, not only for one it has just killed. The port
  had cleaned the body inside the killing blow (`Dies`), so a body a companion's mishap had left on the list
  (mdrcharSetDead alone) and a group then picked kept its poison. The cleanup is now at the check itself; `Dies` is
  mdrcharSetDead alone.
- **Arriving dead runs sub0930's head.** A character loaded dead gets `sub0930 Me` from sub14E0
  (MAINMOD2.BAS:3181-3182), and Wait for Rescue ends in it (FRMDUNM.BAS:194). The port's city-hosted dead screen
  had put up sub0930's screen and boxes without its head, so a body that died on a path with no sub0930 was raised
  by a pending raise (fn16B5, offered from that screen) still poisoned, diseased or holding its companions. The
  load, the city's wait and the return from the dungeon's wait now all go through `ArriveDeadSub0930`: the head, the
  save (fn04ED, 3034), then the screen and its boxes.
- **What still keeps an ailment, as the original does.** A death that is `mdrcharSetDead` alone - a companion's
  mishap (MONSMOD.BAS:901-1305), the chest trap (3654), the list's UPDATE for a member found at nought
  (FRMGROUP.BAS:326-335: a fall in a party, a trap), sub0908's "has disappeared from view!" (MULTIMOD.BAS:57-61) -
  leaves the body as it lay. None of the raises clears anything: fn16B5 (314-380), raiseComplications
  (1642-1685) and the own-body morgue (3087-3118) have no Ailments line. So such a body, raised by joining a party
  (the 'a' key, MAINMOD2.BAS:3478-3499, which loads it with fn04CD and calls fn16B5 with no sub0930), comes back
  with its poison in the original too - until the next city arrival (FRMCTYM.BAS:113-130) or a cure. Raised any other
  way it has been through an arrival first. The port's own-body morgue had called a `CureAll` of its own after the
  raise, which cleared the flags and left PoisonLevel; it is gone, the arrival and the city rest that follows do it.
- A body saved by an older build with its poison on it is cleaned the first time it is loaded or waited out; one
  that is raised by a join instead keeps it, as above.

**The dungeon's opening delivers the body its character carries - corrected 2026-09-27.** frmDungeonMain's Form_Load
runs sub09CC and then sub0A7C for the body Chars(Me) carries (FRMDUNM.BAS:282-285), so a body carried in from town is
put down on arrival rather than at the first change of area.

**The help keeps its jumps - corrected 2026-09-27.** MORDOR.HLP's pages link to one another: 103 jumps across 20
pages, the Welcome page's list of topics among them, each kept by the decompiler as a plain HREF to another page.
The bundler had stripped them with the rest of the markup. It now keeps each as a span of the topic's text with the
topic its page became; every one resolves. The help window draws them as WinHelp draws a jump, green (RGB 0,128,0)
with a solid underline, and a click opens the topic, clearing a search that hides it. Tab and Shift+Tab step
through a page's jumps and Enter follows one. The text of every topic and its tab stops are unchanged, and
tabbed rows holding a jump still line up. WinHelp's Back button has no counterpart, as the window has none of
WinHelp's button bar.

**The help lists each of its 73 pages once - corrected 2026-09-27.** The bundler added every page twice: the pages
MORDOR.hhc lists were recorded with one path separator and the pass that picks up unlisted pages looked them up with
the other, so all 73 came back as a second, appended copy under their html titles, and the topic list showed 146.
The paths are now compared alike, so the contents file's 73 entries, in its order and with its titles, are the whole
list. Their text, tab stops and jumps are exactly those of the first 73 topics before, and the 103 jumps each lead to
the one topic their page became. The help still opens at Welcome, and at Races: Statistics, Guilds: Statistics and
Tutorial where Character Creation's buttons ask for them.

**Objects & Spells is drawn to frmCharStat3's form data - corrected 2026-09-28.** The Items page's rows were about 44
pixels apart, from the theme's list padding. The theme's tab strip was 48 pixels tall with a wide underline, so the
first row sat about 90 pixels below the title bar. The window had been laid out in the port's own sizes: a 96-pixel
button column, a DockPanel and 12-pixel text, scaled down only. frmCharStat3's form data in MORDOR.EXE (RCDATA 10)
has the layout, in twips:
- **The client** is 4065x3105 (271x207 pixels). tabItemTab, a VideoSoft IndexTab, is at 30,30 4005x3060, with the
  captions "Items|Spells|Companions" in MS Sans Serif 8.25 and three tabs to the row (property 2C). Each page is an
  Elastic in the tab control's own coordinates: elaTabFrame(0) 75,360 3810x2550, (1) 75,375 3810x2580 and (2)
  135,390 3810x2580. So the tab strip is 22 pixels, and a page starts 24 pixels down the control. game_files/bm14.png
  shows the strip so, with the current tab bold.
- **The Items page.** Form_Load loads forty lblItemStatus and lblItem (FRMCHAR3.BAS:348-351). sub2263 puts each
  lblItemStatus at the one above's Top + Height, and lblItem at the same top (666-678). lblItemStatus(0) is 30,-15
  210x235, centred, with no font of its own, so the form's bold face. lblItem(0) is 285,-15 5000x240, MS Sans Serif
  8.25. A row is therefore 235 twips, 15.7 pixels, with the mark in a column of its own. They sit in picItems inside
  elaItemFrame, 3495 twips wide, which Form_Load sizes to the page's height (352-357). vsbItems is 3510,0 270x2520,
  and its Max is forty less the rows that fit (442), 30 at the default size. cmdCommand(0..3), Equip, Use, Drop and
  Info, are 870x300 at left 2655, tops 1365, 1650, 1935 and 2220. They stand over the list's lower right, as
  bm14.png shows.
- **The Spells page.** lblLabel(4) "Spell Type" 105,210 1020x240, right-justified; cmbClass 1275,180 2190x300. The
  headings are lblLabel(1) "Spell Name" 135,600, (5) "Guild" 1875,600, (2) "Lev" 2295,600 and (3) "Points" 2895,600,
  the last three right-justified. elaSpells is 165,825 3330x1065 with five rows 210 twips apart: lblName at 30,
  lblGuild 1815 (420 wide), lblLevel 2265 (420) and lblPoints 2775 (525), the last three right-justified. lblGuild
  is brought in front of lblName (363), so a long name is cut where Guild begins. vsbSpells is 3495,825 285x1080;
  lblLabel(0) "Spell" 105,2070; txtSpell 585,2025 1890x285; cmdCast 2580,2010 and cmdInfo 3165,2010, both 600x315.
- **The Companions page.** lblCarryInfo is 90,30 3330x210, Arial 8.25 bold. elaElastic, 60,270 3705x2205, holds the
  four picCompanion pictures, with lblCompName and lblCompHits, and cmdCompanion "1" to "4" (Kick Out) at the top
  right. **Drawn since 2026-10-24** - see [the Companions page is the form's](#the-companions-page-is-the-forms-four-portraits-2026-10-24);
  until then the port had a list and Kick Out, Attack, Name and Info buttons in elaElastic's place.
- **The whole form zooms.** Form_Resize ties the height to the width, sizes the tab control to the client (sub07D4)
  and scales every font by the width over the default (sub07C5, FRMCHAR3.BAS:408-444).
  **The port no longer zooms it up (a GUI deviation, 2026-10-21).** A large Objects & Spells drew its text far larger
  than every other window's and still showed only the form's ten rows, Equip to Info over them. Now the drawing is
  scaled down to a smaller window only; a larger one keeps the Options fonts' size and gives the room to the lists
  (`ObjectsForm.Reflow`): the pack list and its bar take the height and width, and with 60 pixels to spare the four
  buttons stand right of the bar instead of over the rows; the Spells list shows whole rows and widens the names; the
  Companions page's right-hand slots and buttons move right and its lower slots down (`CompanionsPage.Grow`). The Spells list also keeps five whole rows where the Options fonts make a row taller than
  the form's 14 pixels, taking the room the form leaves under Cast and Info. At the form's own size the buttons
  still stand over the list's lower right, as bm14.png shows. Mordor.UiCheck checks both sizes.

The port now draws these rectangles at 271x207 in Microsoft Sans Serif 11, in town and in the dungeon alike, and it
scales the drawing to the window both ways (ShrinkToFit.Grow) with no body margin. The pack list is a list of rows
235 twips tall, each a lblItemStatus column with the "*" and the line at 285 twips. Selection, dragging (to a
buffer, to the Bank, onto another slot), double-click Use and the keys are the list's as before. The list's own
scroll bar is hidden, and vsbItems stands beside it at the form's place and follows it. vsbSpells does the same for
the spell rows. The mods' Objects & Spells buttons, Quality of Life's Sort among them, are the form's button size and
stack upwards from Equip, since the form has no room below Info.

The character window's tabs are frmCharStat1's tabStatTab (RCDATA 8): 30,30 3915x5055, the seven captions, four to
the row, so they wrap onto two rows, and picTabPicture 180,510 3555x4335 below them. The same compact strip is used
there, 32 pixels for the two rows, where the theme's two rows had been nearly 100. The pages' own contents are as
they were. What the IndexTab's other properties do is not decoded. The selfcheck reads every rectangle above out of
MORDOR.EXE and holds both copies of the window to them.

**Quality of Life: no "[Weapon]" after the Hands - corrected 2026-09-28.** The slot label after a pack line
(docs/MODDING.md, "Store: slot and worn columns") named the Hands' class, so row 1 read "1. Hands [Weapon]". The Hands
are MDATA3's item 0 in pack slot 0, the character's own and not something worn. They now have no label. The mods
check asserts it.

**DEVIATION: a drag needs the system drag distance - 2026-09-28.** `lblItem_MouseMove` starts a drag on any MouseMove
with the button down (FRMCHAR3.BAS:589-597; its only early return is `If X = m0022 And Y = m0026 Then Exit Sub`, 590, for a MouseMove that has not moved), and the port's `Dragging.Source` did the same. A pack row is 235 twips,
under sixteen pixels, so a click with a pixel of hand-shake in it became a drag: the click was lost, and a shake across
a row boundary dropped onto the next row and swapped two slots nobody meant to move. A drag now starts only once the
pointer has left the system's drag rectangle (SM_CXDRAG by SM_CYDRAG, four pixels unless changed) round the point the
left button went down on, with the button held all the way. It serves every drag source alike: the pack, the spell
rows, the party's names and the Exchange's lines. A real drag behaves as before. Checked on screen, in town and below:
24 row clicks with up to 3 pixels of movement swapped nothing and moved nothing, and a 40-pixel drag swapped two slots,
filled a character-window buffer, and moved an item into the Bank and back.

**A list drag takes the line pressed on, and a double-click's second press drags nothing - corrected 2026-09-28.**
lblItem_MouseMove drags `lblItem(Index)`, the label the pointer is on (FRMCHAR3.BAS:589-597). The port's list sources
(the pack in town and below, the spell lists, the Bank's vault) took the list's `SelectedIndex` instead. That was the
same line while every list was single-select, but the vault's Shift/Ctrl multi-selection makes `SelectedIndex` the
anchor, so dragging the third line of a Shift-selection dragged the first. `Dragging.SourceLines` now reads the
`ListBoxItem` under the press and hands that line's index to the payload; a press on a list's empty space starts no drag.
And a press that is the second of a double-click (`ClickCount >= 2`, or one a DoubleTapped handler has seen) never
starts a drag: the vault's double-click withdraws while the button is still down and the redraw selects the next line,
so a few pixels of movement afterwards dragged a line nobody pressed on. VB3's labels had no double-click to collide with.
Every `Dragging.Source` user gets the double-click rule; the party rows and the Exchange's rows were already per-row.

**vsbItems and vsbSpells follow the list, and move it only when used - 2026-09-28.** `vsbItems_Change` moves the rows
(FRMCHAR3.BAS:872-879) and `vsbSpells_Change` the spell rows (881-912). In the port the list's own scroll viewer is
the one place the position lives; the separate bar shows it and asks to move it. So the link runs one way each way.
The list sets the bar on its own ScrollChanged, range before value, with SetCurrentValue. The bar moves the list only on
its Scroll event, which a person's arrow, page click or thumb drag raises and a coerced or handed-down Value never does.
Before this, any Value the bar took went straight back into the list, and a click on a row could jump the list to
wherever the bar thought it was. Checked on screen: every one of 48 wheel notches moved the list 50 pixels (about three
rows), 40 Equip clicks and 44 row clicks left it where it was, and a track click paged it and the thumb dragged it.

**An open combo box's list keeps its keys - corrected 2026-09-28.** In a combo box every key but Escape is the
control's own (FRMCHAR3.BAS:313-318). An open Avalonia ComboBox moves the focus to the chosen item in its drop-down,
which lives in a popup of its own. That item has no ComboBox above it in the visual tree, so both frames' key previews
took it for a plain control and gave the key to the game. The Down arrow meant for the Spell Type list turned the party
round, and the list did not move. The previews now pass over anything whose logical parent is a combo box, and
anything in a popup (`KeyOwner`). Checked on screen: with the Spell Type list open, two Down arrows and Enter chose the
type two below, and the party kept its facing.

**The Companions page's rows are two lines - 2026-09-28** (superseded 2026-10-24: the list is gone, and the page is
the form's pictures and labels). The list is the port's own, in elaElastic's place, at the
form's button size (170 pixels wide). Each row was one line, the name padded to 22 characters, then the hits and the
binding. At that width the hits were cut after two digits and the binding was not seen at all. The row now has the
number and name on one line, then the hits and the binding indented below, wrapping inside the list. The words are
unchanged (sub0B82, MAINMOD2.BAS:4704-4745).

**The Store's scroll bar has its own strip - corrected 2026-09-28.** The Buy list runs to vsbItems' right edge
(ServiceForms.Store.ListArea). The theme's grid lays its rows under its whole width, draws its scroll bar over their
right end, and hides the bar to a hairline until the pointer is over it. So the counts' E column sat under the bar,
and a hover covered it. The grid now ends its columns with an empty one, vsbItems' 270 twips (18 pixels) wide, so the
Item column stops where elaItem does. The bar is that width and always drawn, as vsbItems is. This holds with the mods
on or off.

**DEVIATION: the Bank has Deposit and Withdraw buttons - 2026-09-28, asked for by the user.** frmCityBank moves items only
by dragging (FRMCITYB.BAS:172-206, FRMCHAR3.BAS:530-561), which is still there. The port adds:
- **Withdraw**, in elaElastic(3) right of Info (1620,1920,1395,300 twips: Info's height, out to 60 twips short of the box
  as lstItems is). It takes every selected line, in list order, each into the first free pack slot.
- **A double-click** on a line withdraws that line.
- **Shift/Ctrl multi-selection** in the list (lstItems is single-select), for Withdraw. A drag still takes the line
  pressed on.
- **Deposit**, under elaElastic(3) and left of Exit (2895,3585,1485,330: its left lined up with Info's, Exit's top and
  height, 1485 wide - 45 narrower than Info's 1530 - so it ends 45 twips short of Exit), in space the form leaves empty. It deposits the line selected on the Items page of Objects &
  Spells.

The form's own controls are where they were. The rules are the drag paths', run through the same code, not a copy:
- Deposit and a pack line dropped on the list are one routine. The Hands never go in; an equipped item gets "Cannot put
  equipped items in the bank!"; a full vault (forty) gets the "Out of room!" box; otherwise the first empty vault slot,
  holes included. The buffers are left alone, as the drop leaves them (201-203).
- Withdraw and a double-click call `TakeFromVault`, the drop on a pack slot, with the first free slot
  (`Character.FirstFreeSlot`, the loop sub14B5's vault branch uses). What the character can make of the item now is read
  again, "You have re-identified the retrieved item as: ..." said and the Library told (FRMCHAR3.BAS:543-551); buffers
  naming the slot let go (553-558).
- A full pack is the vault branch's own box, "SORRY!" / "X has no free slots!" (MAINMOD2.BAS:2985), since the drag path
  never meets one. A multiple withdrawal stops there, the rest left in the vault.
- Each move is saved, and the Bank redrawn.

**An open Store hears of both - the timing corrected 2026-10-18, the effect since 2026-10-19.**
lstItems_DragDrop sends `frmCityStore!lblCall = "UPDATE"` as the first thing it does for every pack line dropped - the
Hands, an empty slot and an equipped one alike - before anything moves (FRMCITYB.BAS:179-180), and lblItem_DragDrop
sends it at its end for a vault line too (FRMCHAR3.BAS:583; the equipped refusal's Exit Sub skips it). The Store's
`lblCall_Change` (FRMCTYS.BAS:479-502) empties its typed boxes and captions (ID, value, uncurse, combine) and redraws its
shelf. Both paths call `_storeUpdate`, and since 2026-10-18 the deposit calls it first, as the original does.

**The town's services are windows of their own, up together - corrected 2026-10-19.** frmCityMain shows every service
form modeless (`frmCityStore.Show m00B0`, with `Static m00B0` never set, FRMCTYM.BAS:183-203). The port had drawn them
all in one service window, so opening one shut the last, and the messages one service sends another found nobody.
Now (CityWindow.Services.cs):
- **Each service has a window of its own**, placed from its own table entry as before, and any number can be up: the
  Store, the Confinement, the Bank, a Guild, the Morgue and the Seer. The pages in them are built as they were.
- **A picture clicked for a service already up** brings its window forward and makes it the active form, and builds
  nothing again: Show on a loaded form runs no Form_Load, so what is typed and chosen in it stays.
- **The window just opened is the active form**, and stays so until another window is clicked; the town's own Alt
  pictures (FRMCTYM.BAS:35-58) answer while the town is the active window, with services up or not. A service's
  Enter/Escape buttons and access keys answer only while its own window is the active one.
- **Closing one** hands the activation to the topmost service still up, or the town.
- **What one tells another now arrives:** a Bank deposit clears the open Store's boxes before anything moves
  (FRMCITYB.BAS:180); a pack drag tells the Store (FRMCHAR3.BAS:583); the Bank's three Party Options tell an open Guild
  "UPDATE" (FRMCITYB.BAS:64), which puts it back on the current guild and tests Make Level again (FRMCTYG.BAS:671-686) -
  the port had not sent that one at all; a charge paid anywhere in town (charPay's `mdrcharStats False, 10`,
  MAINMOD2.BAS:708) tells an open Bank "GOLD" (5616-5620), which redraws its status line - sub2884 alone
  (FRMCITYB.BAS:154-155) - with the bank gold the charge drew on; a switch of character tells every one that is up
  (MULTIMOD.BAS:633-637), as before.
- **Kept:** the Library and the Guild Log, which the Guild opens modally over itself (FRMCTYG.BAS:170-177), are still
  drawn in the Guild's window and give it back on closing; they are not modal here. The dead screen is one more such
  window (see T1 in the dead screen entries).
`Mordor.UiCheck` drives this headless: the Store and the Bank up together, the Store's update alive while the Bank is up,
the Store's picture bringing the Store forward without a rebuild, and the Bank shut leaving the Store up.

**The dead screen stands where frmDungeonMain does - corrected 2026-10-19.** sub14E0 shows frmDungeonMain for a
character loaded dead, at any level, and frmCityMain only for one alive in town: the one or the other, never both
(MAINMOD2.BAS:3170-3180). A raise at level 0 unloads frmDungeonMain and shows frmCityMain (sub14CC, 3150-3152). The port
had put sub0930's screen up over a town that was still there, its pictures and its Alt keys still answering (each sent
the dead back to the screen). Now the screen is a window of its own, captioned "Dungeon (Name)" and placed as the
Dungeon is, and the town's window is not shown while it is up: nothing of frmCityMain - its pictures, its Alt keys
(FRMCTYM.BAS:35-58) - can be reached, and the keys go to fn1521's handling, as frmDungeonMain's Form_KeyDown sends every
key at level 0 (FRMDUNM.BAS:207-213). Closing it after a raise brings the town back. frmDungeonMain's two timers do
nothing at level 0 (timAilment_Timer leaves at once, FRMDUNM.BAS:438-441; the fight clock is off, sub0930), and the
screen has none.
- **Only for a character alone (review fix 2026-10-19).** sub0930 puts its screen up only with nobody else loaded:
  otherwise it tells frmParty "UPDATE" and leaves (MAINMOD2.BAS:3035-3038), and that UPDATE unloads a member at
  nought from a party (FRMGROUP.BAS:321-346). imgPicture_Click asks nothing about death (FRMCTYM.BAS:183-190). So the
  screen is owed only while the character in command is dead and alone; whenever that stops - company joins, the
  character is raised, a switch puts a living one in command - it comes down and the town is shown, as sub14CC shows it (MAINMOD2.BAS:3150-3152) and
  mdrmulSwitch's town branch puts it forward (MULTIMOD.BAS:631-637). A dead member in
  command with company uses the services like anyone. The port had hidden the town for good once the screen was up in a
  party, and put the screen up for a dead member in command whenever a service closed.
- **Visit the Morgue is the dead one's.** cmdWait_Click acts only for a dead character in command (FRMDUNM.BAS:155) and
  ends in sub0930, which offers the morgue to a character alone; the button now offers nothing unless the one in
  command is the dead character the screen was built for, alone. The port had priced the screen for the dead character
  and then paid and raised whoever was in command at the click.
**Still the port's:** the screen is not the dungeon window itself, so the dungeon's own toolbar and
buttons are not about it; and which music plays is the sound work's. `Mordor.UiCheck` checks the town hidden and back.

Checked on screen (mods off): an equipped Cloak refused with the line; the Liquid deposited by button into the vault's
hole; a double-click took it back to slot 3 and re-identified it as Potion; Ctrl-click and Shift-click selections
withdrew two and three items into slots 7-11 (one re-identified as Mace); drags both ways still worked; a full vault
gave "Out of room!"; withdrawing 35 into a pack with 33 free slots filled it and gave "Tester has no free slots!".
`selfcheck` checks the vault rule and that the buttons use the drag paths' code and clear every control of the form.

**frmInteract's name box is wide enough for its sentences - corrected 2026-09-28.** The box was 260 pixels and cut
"No other characters in the area!" (the Morgue's Raise Character with nobody to raise) to "No other characters in
the...". The dialog is now 420 pixels wide and the combo fills it, the text box without the theme's minimum width and
padding; the longest sentence, "No characters available for this selection!", fits on one line, and a greyed sentence
wraps rather than being cut if a larger font would push it over. The drop-down list is as wide as the combo. Checked
on screen for Raise Character and Hire Rescuers.

**Deviation, at the user's request: the Store's rows are taller than elaItem's - 2026-09-28.** frmCityStore's
elaItem rows are 210 twips (14 pixels) in MS Sans Serif 8.25. At the form's own height the theme's larger bold text
was crowded and clipped. The port drew them 16 pixels high with 12-pixel text; since the Options fonts are applied
(next entry) the text is the form's own size again and a row is its fonts' line and three pixels: 17 pixels with the
defaults, whose Courier New counts are 12 pixels tall. About seven rows show instead of nine.

**The Options fonts are applied - corrected 2026-09-28.** The Options window saved its two fonts (MORDOR.INI
[Fonts]: "Pro" Courier New 9 and "NonPro" MS Sans Serif 8 by default, MAINMOD.BAS:1252-1265) and nothing read them.
The port's text was a mix of the theme's face at several sizes, Cascadia Mono, and faces and sizes written into each
window, so sizes looked uneven: in the Store the item names and the Slot and Worn columns were larger than the counts
and the labels. The original gives every form's text its font through three routines, and the port now does the same
for every game window, through one place (`Mordor.App/UiFonts.cs`, the rule in `Mordor.Core/DesignFonts.cs`):
- **sub0784** (MAINMOD.BAS:1862-1898), from Form_Load: nothing at all while all four settings are the defaults.
  Otherwise the form and each control but lines and scroll bars: a Courier New one takes the Pro face and ProSize, an
  MS Sans Serif one the NonPro face and NonProSize - in that order, the second test reading the name the first may have
  set. Any other face (Arial, Symbol, Times New Roman) is left alone.
- **sub07C5 / sub0760** (1925-1975, 1750-1848), as a form is shown and on every resize: a control whose Tag starts with
  ":" takes that size; one whose face is now the Pro face takes ProSize, and every other control NonProSize, all times
  the width over the default. The width ratio is the port's zoom (ShrinkToFit, the Viewboxes), which scales the
  drawing and its text together.
- **sub0802 / sub07ED** (2001-2100), once at load: the sizes alone, never the faces.
- **Which form gets which.** Read from each form's own module and held to it by `selfcheck`: sub0784 alone -
  frmCharStat4 (Messages), frmAutoMap, frmInteract (the name and password boxes, and the port's pick-a-name lists in
  its place), frmPartyOptions; sub0784 and a sizing pass - the six services, frmParty, frmCharStat1 (the character
  window), frmCharStat3 (Objects & Spells), frmCityMain, frmDungeonMain, frmExchange; sub0802 alone - frmLibrary,
  frmGuildLog, frmHall.
- **Each control's design font** is its font in the form data in MORDOR.EXE: a property holding the face's name, the
  size as a four-byte float (00 00 04 41 is 8.25) and a byte whose lowest bit is bold; a control with none has VB3's
  default, MS Sans Serif 8.25 bold. That holds even on frmCharStat1, the one form whose own header stores a font
  (Courier New 8.25 bold, RCDATA 8 offset 56): bm13.png shows its unfonted captions (lblStatInfo, lblRaceInfo) in a
  bold sans serif, not Courier New. The Tag is a string property; the welcomes of the Guild, the Bank, the Morgue and
  the Seer are ":12". `DesignFonts` lists every control whose text the port draws with a font of its own, and the
  default-font ones its own text stands for; `selfcheck` reads each back out of MORDOR.EXE (`Mordor.DataDump/FormFonts.cs`,
  also the `formfonts` probe, which prints every form's), and checks that every control of `ServiceForms` with a font
  of its own is listed.
- **What the defaults give.** A zooming form's MS Sans Serif text is 8 points, which Windows draws at 11 pixels as it
  does 8.25: the same as before. Its Courier New text is 9 points, 12 pixels: the Store's counts and headings, the
  Guild's stats, Confinement's counts, the figures on the character window's Stats page (lblStat; the page's captions
  are MS Sans Serif bold, as below). They had been 11. Courier New is a light, narrow face,
  and at 12 pixels it stands beside MS Sans Serif at 11 as it does in the original. A form sub0784 alone styles keeps
  its design sizes. Arial keeps its face at NonProSize (the Store's Item and Cost fields, the Companions page's line).
  frmExchange's name panels, designed 9.75, are 8 points after its sub07ED; bm1.png shows frmCreateChar, which runs
  sub07ED too, with its 9.75-point headings ("Character Name", "Stats") no larger than its 8.25-point labels.
  frmHall's heading, designed 12 points with no Tag, is 8 points after sub0802 likewise.
- **The Messages list, the automap and the Companions list** were the frame's Cascadia Mono. lstMessages and the map's
  labels are MS Sans Serif, and are now. The automap's numbers are printed on the form (Me.Print, sub2E18) in its own
  font, MS Sans Serif 8.25 bold; they had been the theme's at 9 pixels. The pick-a-name lists and the password box
  (frmInteract, fn170D) and Party Options were Cascadia Mono too.
- **The fonts are read as a game window is built.** Form_Load disables the Options window's font buttons while a
  character is in play (FRMOPTIO.BAS:330-338), so they only change at the main menu; the next town or dungeon, and
  the Hall of Records, are drawn with them.
- **Faces Avalonia cannot draw.** MS Sans Serif is a bitmap font, which it cannot use; Windows draws it as Microsoft
  Sans Serif, and so does the port, but for its bold, which is Tahoma Bold since 2026-09-29 (below, "Bold MS Sans
  Serif is drawn in Tahoma Bold"). A chosen face that is not installed falls back to Microsoft Sans Serif.
- **A hand-edited MORDOR.INI.** An empty `Pro=` or `NonPro=` is the default face, as the load writes Courier New /
  MS Sans Serif back and reads the face again (MAINMOD.BAS:1256-1265); it does not read the size again, so the size
  stays what was read. A size of nothing or less is an error VB's On Error Resume Next steps over (sub0784,
  MAINMOD.BAS:1865; sub07C5's `LDD52: Resume Next`, 1980-1981): the face is still set and the control keeps the
  size it had. The port does the same, and never measures or draws text below one pixel.
- **Not reached yet** (the port's own layouts, not their forms): the Options window itself, whose two labels show the
  fonts, the Control Panel, character creation, the title and main menu, the About box, the help viewer, the Mods
  page. `selfcheck` lists these and fails on any other window that sets a font of its own. The MsgBox stand-ins
  (Tell, Confirm, OriginalBox) are in Windows' message font since 2026-09-29 (below).

**Deviations that come with it**, for a larger font than a form was drawn for:
- **Rows grow.** Every list's rows are its fonts' line and three pixels (`UiFonts.RowPad`), so a larger font makes
  taller rows and fewer of them show, instead of cut ones. With the defaults: the Guild's, the Bank's, the Library's
  and the Guild Log's lists 16 pixels (VB's were 13), the Store's 17 (see the entry above), Confinement's creature rows
  17 (the form's 15; as many of them as fit where the form's nine are, eight with the defaults), the Items page's 16
  (the form's 15.7) and the Spells page's 16 (the form's 14). Party Information's rows stay lblName's 13 pixels, the
  line of the default face: its buttons sit below four of them in a form that keeps its proportions. A larger font
  makes the header and each row as much taller as its line, and the drawing and the window's height grow with them.
  The Exchange's pnlItem rows are 16, and grow the same way.
- **A caption is let out, not cut.** A form label whose caption no longer fits its box in a larger font is widened to
  its longest word (away from the side it is aligned to) and let down to its lines (`FormPage.LetOut`); the other
  controls keep their places. With the defaults a few labels designed a pixel or two shorter than MS Sans Serif's
  13-pixel line are let down to it, which moves nothing that shows. The Store's counts column widens to hold "9 9 9 9"
  in the chosen Pro font.
- **A form drawn to its data grows with a larger font - 2026-09-28.** Letting a caption out moved nothing else, so a
  larger chosen font still ran captions together: with Consolas 10 and Segoe UI 10 the Store's "Identify, Realign &
  Sell Items" wrapped onto its panel's "Item", Confinement's "Companion" and "Comp." ran into their fields, the Bank's
  "Pool & Deposit" and "Deposit/Withdraw Items" were cut, and the Hall's record lines lost their ends. The original
  sizes each font by the form's width alone (sub07C5, sub0802), so at a larger chosen size its captions overflow
  too. Now every page drawn to its form data - the six services, the Library, the Guild Log, the Hall of Records
  and the Exchange - is laid out larger by as much as the chosen fonts draw larger than the defaults
  (`UiFonts.FormGrowth`): across, by the width of a line of sample captions in each of the four design faces
  (MS Sans Serif and Courier New 8.25, bold and not) through the form's pass, against the same line in the
  default fonts; down, by the ratio of their line heights; never less than one. Every rectangle of the form data
  goes through it (`FormPage.X`/`Y`, `VbForm.At(..., grow)`), and the window is the grown client. With the default
  fonts it is exactly one each way, and every page is the form's own size to the pixel, as before. With Consolas 10
  and Segoe UI 10 the pages are noticeably larger, more so down than across (Segoe UI's line is tall for its
  width). Confinement's counts are set right in their box, under the right-justified "G N E" (since 2026-09-29 with
  the defaults too, below).
- **Party Information's mod columns hold their headings, and keep off the edge - 2026-09-28.** A mod column is as
  wide as its mod asks, or as its heading in the drawing's font and 4 pixels when that is wider
  (`ModSlots.PartyColumnWidth`): "HP/SP" had run into "Option" in Segoe UI 10. The heading and the rows take the
  same width. And the drawing is 4 pixels wider again after the last mod column (`ModSlots.PartyRightRoom`, the
  ShrinkToFit.ExtraWidth the columns already widen it through), as Leave is kept 4 pixels in from the right: the
  Next column's figures ("230K") had run against the window's right edge in the dungeon's narrow Party window. With
  no mod column shown nothing changes; with the defaults the columns are the widths their mods ask.
- **The Spells list stops on whole rows - 2026-09-28.** vsbSpells' Value is a whole row (FRMCHAR3.BAS:881-912); the
  port's list scrolls by the pixel, and a spell brought into view at the bottom (the dungeon's Teleport) left the
  lower half of a row showing at the top, under "Spell Name". The rows now have the room below the last one that
  makes the furthest scroll a whole number of rows, and a scroll that stops between rows goes to the nearest
  (`Core.WholeRows`, `ObjectsForm.LinkScrollBar`), in town and in the dungeon. Unscrolled, the list is as it was.
- **The tab strips** (Objects & Spells, the character window) are never shorter than the font's line.
- **The toolbar.** frmMain is styled by none of the three routines, so the original's toolbar keeps Courier New and
  MS Sans Serif 8.25 whatever is chosen. The port gives its readouts, its resistances and the mods' buttons the
  Options fonts as sub0784 would, so the strip matches the windows under it.
- **The mods' text** (docs/MODDING.md: the Store's Slot and Worn columns, the Party Next and HP/SP columns and their
  headings, the Hide unusable box, the Objects & Spells and Bank buttons, the Combat Log window) is in the NonPro role:
  the face and size of the list or form it sits on. The Store's Slot and Worn headings are the exception since
  2026-09-29: they stand on Label9's heading line, so they are in its Courier New, the Pro role, as "Item" is.

**The Store's "Hide unusable" box fits - corrected 2026-09-28.** The Quality of Life mod's box sat on the Buy Items
heading's line, right-aligned in the room to the page's right edge, and showed as "Hide unusa" with its square cut by
the Buy Items panel: the theme's check box is a 20-pixel square at the top of a 32-pixel row with a 120-pixel minimum
width. It is now drawn as VB draws a check box (`FormPage.Compact`): a 13-pixel square beside its caption, as tall as
the caption's line, in the Store's own face. It fits inside the page with the mod's columns shown or not.

**The Guild's Default and Cancel buttons - corrected 2026-09-28.** frmCityGuild's form data makes cmdMake the Default
button and cmdExit the Cancel button. The port's Guild took neither key. While the Guild is the active window (it is
opened from the town, and stays so until another window is clicked) and no box has the keyboard, Enter now presses
Make Level when it is enabled and Escape presses EXIT. Checked on screen: Enter made a level-64 Nomad level 65, and
Escape closed the Guild.

Checked on screen for the fonts, with the defaults and with Pro = Consolas 10 and NonPro = Segoe UI 10 in a scratch
settings.ini: the town with every service, the Library, the Guild Log, the Hall of Records, the Exchange, the dungeon,
Party Information, the character window, Objects & Spells, Messages and the toolbar. With the defaults the windows look
as they did but for the Courier New text at 12 pixels, the rows above and the Hall's heading; with the chosen fonts
each is in Consolas or Segoe UI at 13 pixels - the Library, the Guild Log and the Hall in their own MS Sans Serif at
13, as sub0802 sizes and never renames - the rows and Party Information taller, and Hide unusable whole.

Checked again on 2026-09-28 for the growth, the Party columns and the Spells list, by PrintWindow captures of Release
builds of this change and of the commit before it: with the default fonts every service, the Library, the Guild Log,
the Hall, the Exchange and the town are the same to the pixel, but for the Party window's mod columns (4 pixels more
room at the right, so the drawing is scaled a hair smaller) and the dungeon's Spells list brought down to Teleport,
which now shows Ethereal Portal to Teleport whole where the lower half of Feather Essence had shown under the
heading. With Consolas 10 and Segoe UI 10 no caption on those pages wraps onto or runs into another, and "Option"
and "HP/SP" stand apart. The dungeon's action buttons (frmDungeonMain zooms with its window, as Party Information and
Objects & Spells do, so they are not grown) hold "Pickup" and "Fight" with little to spare.

**Fonts and spacing in the zooming windows - 2026-09-29.** An audit with the default fonts and with Consolas 10 /
Segoe UI 10 found text cut or run together in the windows that zoom with their frame. With the default fonts every
change below is a no-op but where it says otherwise; each is held by `selfcheck` ("sub150A's arrangement..." and
"the Options fonts: frmDungeonMain's rows and button row...", and the Party Information checks).
- **The monster rows and captions are the font's line** (`DungeonWindow.FitDungeonDrawing`). lblMonster(0..3) are
  195 twips tall and 210 apart, lblCharacterHit 210 and lblMonsterHit 195: 14, 14 and 13 pixels, the default face's
  13-pixel line. In Segoe UI 10 the descenders ("Large", "Clean-Ups") and lblCurrentGroup's "[" were cut. Each row is
  now the line and a pixel, each caption the line, where that is more than the form's, and picMonsters and all under
  it that much lower. A deviation, and one that must not make the Dungeon window taller - that would run it over the
  windows the player keeps under it - so the drawing grows across by as much in the form's proportions (the room
  goes to picMonsters, lblSquareInfo, picTrap and the button row; the view and its arrows keep theirs) and the
  window zooms it a little smaller.
- **The button row is framed as VB3 draws a CommandButton** (`Button.frame`, DungeonWindow.axaml). The form data
  (RCDATA 22, in picPicture(1) at 45,2715) puts Pickup (3015, 600 wide) 15 twips over Drop (3600) and Fight (4230,
  500) 15 twips over Spell (4710), and the ten buffers edge to edge; game_files/dmainw1.png shows each pair parted by
  the buttons' black frames. The theme's frame is see-through, and each pair read as one button. The positions are
  the form's, to the pixel (checked against those rectangles); only the one-pixel black frame is added, which shows
  at the default fonts too. "10" fills its 15-pixel buffer in dmainw1.png as it does here. In a larger font a
  caption wider than its button ("Pickup", "10") had run over its edges: each command is made as wide as its
  caption, its frame and two pixels each side, in the form's order and with its gaps, against the right as the form
  has them, and the buffers take the room left (`FitButtonRow`; a deviation, larger fonts only).
- **Party Information's rows are laid out cell by cell** (`CityWindow.PartyRowGrid`, one builder for town and
  dungeon). Each label has its own width and aligns its text in it, as its Alignment has it - lblHits and lblStatus
  right, lblSpells centred, lblOption right - so a row's content no longer moves its cells: the selected member's row
  (bold name) and a row with a five-digit Hits had been laid out a few pixels off the others. A mod's cell is its
  column's width the same way (`ModSlots.PartyRow`). With the default fonts the cells now end exactly where the form's
  labels do, which moved lblStatus's "OK" about a pixel right and lblOption's "None" a fraction left.
- **Five-digit hits are shown whole.** lblHits is 660 twips, 44 pixels, and right-justified; "30000/30000" is about
  62 pixels in MS Sans Serif 8. VB draws a caption wider than a label that is not AutoSize cut at the label's edges,
  so the original cannot show it whole either; the port had cut it at the right, "30000/30(". Now the figure reaches
  left over the name's room as far as it needs and is never cut (a deviation): lblHits is the one in front of
  lblName, so the name is what gives way.
- **A long name keeps clear of the hits.** lblName is cut where lblHits begins (FRMGROUP.BAS:371), and still is, but
  3 pixels sooner (`PartyNameGap`), or 3 pixels before a five-digit figure's own left: "Thorgrimbalds" had run up
  against the column. With the default fonts a name of more than about 12 letters shows one letter fewer.
- **The Party drawing grows across with a larger font** (`CityWindow.PartyGrowthX`, `UiFonts.FormGrowth` across, as
  the pages drawn to their forms grow): every column, margin, left and button of frmParty is that much wider, and so
  is a mod column's width (`ModSlots.PartyColumnWidth`, the mod's width being in the default fonts' pixels) and the
  room after it; the window keeps its size, its height for a width taking the growth both ways into account
  (`PartyAspect`). The headings had run together in Segoe UI 10 ("Hits Spells Status", "Option HP/SP"), and the bold
  "!Pinned" had filled its bar. With the default fonts the growth is exactly one.
- **The Automap and a mod's Party columns** (`WindowLayout.Arrange`, `partyWidened`). sub150A puts the Automap one
  pixel right of the character window (MAINMOD2.BAS:3241), and Party Information (299 pixels) is wider than the
  character window (274), so the Automap - shown after it (sub14E0, MAINMOD2.BAS:3163-3164), so in front - covers the
  end of "Option" in the original as in the port, and that is kept. A mod's columns are drawn at that right edge and
  were all under it ("Next" showed as "N"). While a mod shows a Party Information column, a new character's Automap
  goes one pixel right of Party Information instead, as far as Objects & Spells beside it leaves room (at 800x600
  only four pixels further). A deviation for the mods alone; a saved layout is never moved, and with no mod column
  the arrangement is sub150A's exactly. The arrangement also takes a window's own height for its width where a larger
  font made its drawing taller (Party Information), so nothing laid out under it is overlapped.
- **Objects & Spells' bottom line** (`ObjectsForm.FitSpellRow`). lblLabel(0) "Spell" (105,2070 510x195) runs 30 twips
  into txtSpell (585,2025): the default caption stops short of the box, and Segoe UI 10's touched it. In a larger
  font txtSpell now begins 4 pixels after the caption and keeps its right edge, and is as tall as its line and frame
  need; and it has no scroll bar at any size (the theme had put a scroll bar's up-arrow in the box). cmdCast (2580,
  600 wide) runs 15 twips over cmdInfo (3165), as Pickup over Drop: the two are framed the same way, at both sizes.
- **The Companions page** (`ObjectsForm.FitCompanionPage`). In a larger font the page - the pictures, names, hits,
  carry line and the four buttons - is laid out `FormGrowth` larger and zoomed back into its room, so a longer name
  keeps to its label and a picture to its 50 pixels. (Until 2026-10-24 this fitted the port's own list and its
  "Kick Out" buttons.)
- **The Quality of Life mod's XP tooltip** is two lines (1.7.1): the progress, then the two amounts. On one line it
  wrapped in Segoe UI 10 and left "pin" alone.

Checked on 2026-09-29 by PrintWindow captures of Release builds of this change and of 34da634, in the dungeon with
the default fonts (mods off and on) and with Consolas 10 / Segoe UI 10: with the defaults the windows are the same
but for the frames on the button row and on Cast and Info, the Party rows' cells (above), a long name one letter
shorter, the Companions buttons 3 pixels wider, and, with the mod's columns shown, the Automap beside Party
Information rather than over it. With the larger fonts the monster rows, "[", every button caption, "30000/30000",
the Party headings, the Spell box, the Companions line and the tooltip are whole and apart.

**Bold MS Sans Serif is drawn in Tahoma Bold, and a round of spacing - 2026-09-29.** An audit of the windows with the
default fonts and with Consolas 10 / Segoe UI 10 found text run together or against its box. What changed:
- **The bold face.** Microsoft Sans Serif, the TrueType MS Sans Serif the port draws the forms' face in, has no bold.
  Avalonia made one up by thickening each letter both ways without widening its advance, so the bold captions - the
  form's default face, and so most of every service's labels, the Char page's headings ("Mon Quests:", "Guild A/D:"),
  the Store's usable rows - came out smeared, their letters touching. Windows made the original's bold by drawing the
  bitmap letters twice a pixel apart, each letter a pixel wider. Grey antialiasing looked the same, and aliased text
  dropped the made-up bold altogether. The port's MS Sans Serif is now a font collection of its own
  (`Mordor.App/SansStandIn.cs`): the plain letters are Microsoft Sans Serif's, the bold ones Tahoma Bold's, a real
  bold drawn for these sizes. Tahoma is Windows' own stand-in for MS Sans Serif in dialogs ("MS Shell Dlg 2"), and
  its bold is about as wide as the original's doubled letters: at 11 pixels "Uncurse Items" measures 81 pixels in
  Tahoma Bold and 71 in the made-up bold (the plain width), where the doubled letters make about 84 (71 and a pixel
  for each of its 13 characters). Tahoma Bold sits about a pixel
  lower in its line than Microsoft Sans Serif, so a bold caption is drawn that pixel higher (`UiFonts.BoldLift`) and
  keeps the plain face's line for its height (`UiFonts.LineHeight`): rows and labels are the heights they were, and a
  heading stands level with the plain value beside it. With Tahoma Bold missing the bold is the made-up one again. A
  chosen NonPro face other than MS Sans Serif is drawn as it is. Since the default bold is now wider, the growth of a
  form for a larger chosen font (`UiFonts.FormGrowth`) is a little less than it was.
- **Confinement's counts.** They were set left in elaCompInfo's box, against its left edge with the defaults, while
  lblLabel(4)'s "G N E" is right-justified over them, so the letters were not over their numbers; with Consolas 10 the
  box was narrower than "0 2 0" and the last digit was cut. The column is now the form's box, or "9 9 9" in the chosen
  Pro font with two pixels at each side when that is wider (kept to the box's right edge, the names' box giving way on
  the left), and both the counts and "G N E" are set right two pixels in.
- **The item's Misc page.** mdrcharShowInfo puts the stat names at every second column from 3 and the Req and Mod
  figures a column after each (MAINMOD2.BAS:5085-5095), counted in the widest character. In Microsoft Sans Serif the
  names ran together ("Wis Con Cha Dex"), "Mod" met its first figure in Segoe UI, and "at the casters Spell Level" ran
  into the Allowed Guilds column. The page now widens its column from the widest character to as much as keeps each
  piece a letter clear of the next on its row, so the original's columns are kept, scaled to the font, and each Req
  and Mod figure is centred under its stat's name rather than a column after its start (a deviation). The spell and
  companion pages and the other tabs are drawn as before.
- **The Char page.** On screen a value's or heading's leading space is left off (Str$'s, and " Item A/D:"), so Died
  and Comp stand under Kills and Item A/D under Stats A/D; CHARSTAT.TXT keeps them.
- **The Store's headings.** "Item" and "U G N E" are Label9's Courier New; the Quality of Life mod's "Slot" and "Worn"
  were in the list's face, so one heading line had two faces. A mod column's heading is now in the heading line's
  Courier New (the Pro role), set in as far as its cells; the cells are as they were.
- **The Hall of Records.** lblRecordChar and lblHistoryChar reach their panel's edge in the form data, and drawn inside
  its sunken border their right-set lines touched it. They are now set in from the right as far as the headings are
  from the left (lblRecordStat's 60 twips, 4 pixels).
- **The service lists** (the Guild's, the Bank's, the Library's, the Guild Log's) set their text 3 pixels in from their
  line instead of 2 (`FormPage.ListInset`), as far as a VB list sets it in from its outer edge.
- **The Exchange.** frmExchange's right-hand panel (3195 + 3090 twips) and Cancel (5070 + 345) reach the edges of its
  window, which is 6360x5475 twips outside, frame and caption included; the port's 416x361 client cut the panel's
  right edge and put Cancel against the bottom. The client is now the controls with the form's own 60-twip left margin
  past them, right and below: 423x365 pixels with the defaults.
- **The automap.** sub2E18 prints the row numbers at CurrentX -2 (FRMAUTOM.BAS:517); the port keeps a 17-pixel strip
  for them, which "30" and "28" filled in a larger font. The strip is now 17 pixels or the widest number in the chosen
  face and 2 pixels, whichever is wider.
- **The Mods page** keeps its text clear of the scroll bar the theme draws over its right edge.
- **MsgBox.** The town's and the dungeon's Tell and Confirm and `OriginalBox` took their frame's Cascadia Mono. MsgBox
  is drawn by Windows in its message font, which no sub0784 reaches, so never the Options fonts: on the Windows the
  game was made for, MS Sans Serif 8 points, 11 pixels. The boxes are now in the port's MS Sans Serif at 11 pixels
  (`UiFonts.MessageBox`), whatever the Options fonts.

Checked by PrintWindow captures of Release builds of this change and of 34da634, with the defaults and with Consolas
10 / Segoe UI 10: Confinement, the Store, the Guild, the Hall, the Exchange, the item page and the Char page. With the
defaults the bold captions are Tahoma Bold, at the heights and places they had; Confinement's counts stand under
"G N E" clear of both edges; the item page's stat names and figures stand apart; Died and Item A/D line up; the Hall's
lines, the Guild's list and "Stats Required" have room; the Exchange has its margin. With the chosen fonts the same,
and the counts whole. Not yet looked at on screen: the automap's row numbers, the Mods page, the MsgBox font, and the
Options window's "Last Backup" line and the pick-a-name box, which the audit also named and which are unchanged.

**The Confinement's Select Alignment frame comes and goes, and one alignment is taken without asking - corrected
2026-09-29.** elaAlignments.P?C008 is the Elastic's property 08, its Visible. The form data stores it, as 0, on the two
Select Alignment frames (the Confinement's and the Store's) and on no other Elastic, and bm9.png shows no frame. The
Confinement's row click, elaComp_event0000 (FRMCITYC.BAS:302-370), is the Store's elaItem_event0000
(FRMCTYS.BAS:326-392) over the pens:
- G, N and E are enabled where the pen holds that alignment, and m002C is left at the last of them (319-330).
- With more than one, m002C is -1, the frame is shown, EXIT, BUY, INFO and the scroll bar are greyed and the form
  waits until a button sets m002C (331-341); then everything comes back and the frame is hidden (342-347).
- With one, that one is taken at once, BUY and INFO are enabled and the frame stays hidden (349-351). With none, the
  alignment chosen before stands.
- Either way the Companion and "N(x) Cost" lines follow for the alignment taken (352-363).

The port had read P?C008 as a busy flag only. Its frame came up on the first row click, over the list's middle rows,
and stayed up: after a pick, pressing another of its buttons changed what BUY charged but not the Cost shown, so a
choice looked as though it had not taken, and a row with one alignment in stock seemed to want a pick it did not need.
Now the frame is up only while the click waits, as in the original, and a single alignment is taken without it. The
Store already did this; both now go through `AlignmentChoice` (Mordor.Core). The Confinement's wait still holds the
window as the original's does (every other row beeps, EXIT and the scroll bar greyed); the Quality of Life mod's
"Unlocked Store alignment choice" is the Store's only. After a character switch with the scroll bar greyed the frame
now comes up with no wait running (lblCall_Change, 468-470), a fault kept (see "Faults of the original, kept" above).
Selfcheck reads property 08 out of both forms' data, checks the decision on one, two and no alignments in stock for
both shops, and that a pen of one alignment is priced and bought at that alignment.

**Objects & Spells gives the keyboard back after a cast or an Info - corrected 2026-09-29.** Reported: Alt+1..4
stopped switching characters after a visit to the Spells page. frmCharStat3's Form_KeyDown keeps every key but Escape
while a ComboBox, a TextBox or a VScrollBar has the focus (FRMCHAR3.BAS:310-318), and hands the rest to fn1521
(320-330), which passes Alt and a digit to fn15DF (MAINMOD2.BAS:3385, 4222-4230). So Alt+1..4 do nothing in cmbClass
or txtSpell in the original either, and the port keeps that rule (`KeyOwner`). What differed is where the focus goes:
- **A tab click** sets no focus in the original (tabItemTab_event000D, FRMCHAR3.BAS:830-843). In the port a clicked
  TabItem takes the focus, and the previews give its keys to the game. A headless Avalonia 11.2.3 run of the port's own
  MdiFrame and IndexTab showed this for all three tabs, whether the focus had been in the spell box, the Spell Type box
  or elsewhere, and Alt+2 reached the window's preview unhandled. A tab click never put the focus in a box. Only
  lblCall "SPELL", from the Spell button and the c key, puts it in txtSpell (454-460). Clicking the Spell Type box or
  the spell box puts it there too, as a click does in Windows.
- **Cast** ends in sub0775, `gv1BFC.SetFocus`, whether the spell became the order or was cast at once (FRMCHAR3.BAS:183,
  187). The port did not do this, so after Enter in the spell box the focus stayed in txtSpell. Alt+1..4, the arrows
  and every other game key then did nothing until Escape. The dungeon now returns the focus to the main window when the
  cast is over (`CastThenSub0775`). The town's "Cannot cast spells while in the city!" is an Exit Sub before any sub0775
  (156-159), and it still leaves the focus where it was.
- **Info** on a spell (299-301), on an item (209) and on a companion (759) puts the focus on
  frmCharStat1!picTabPicture, whose form hands every key to fn1521. The port had only done this for the dungeon's item
  Info. The rest now do it in town and below (the frame's `Focus()`, as the dungeon's item Info already did).
- **Kick Out** ends in sub0775 once its question is answered, yes or no (271-280). The two refusals before it are Exit
  Subs and move nothing (261, 265).

The selfcheck holds each of these to the source, and holds that only the Spell button's path focuses the spell box and
that nothing focuses the Spell Type box. Not yet seen on screen. To check: after the Spell button, a name and Enter,
Alt+2 switches; after spell Info, Alt+2 switches; a click on each tab, then Alt+2, switches.

**The Spell Type box keeps the keyboard after a pick - looked at 2026-10-12, as the original.** Reported: on the
Spells page, after choosing from the Spell Type box, Alt+1..4 no longer switch characters. The original does the same.
cmbClass_Click (FRMCHAR3.BAS:55-149) lists the class and calls no SetFocus at all, its own or the main form's. The click
that opened the box gave it the focus, as a click on a combo box does in Windows, and it keeps it. frmCharStat3's
Form_KeyDown then leaves every key to a ComboBox and acts only on Escape, which is `gv1BFC.SetFocus` (313-314). Only
its last two branches pass a key on to fn1521 and fn15DF, where Alt and a digit set the Party window's lblCall
(319-330; MAINMOD2.BAS:4222-4230). This is not a rule the forms share. The Store, the Companion store, the Bank, the
Morgue and the Guilds hand a ComboBox's or TextBox's key to fn15DF (FRMCTYS.BAS:407-425 and the rest; the city's
`ServiceFieldKey`). Objects & Spells does not. The port already behaved this way. A headless Avalonia 11.2.3 run showed
that after a mouse pick from a ComboBox's list the focus is on the ComboBox, and that Alt+2 arrives there. The town's
`OnKeyDown` and the dungeon's give such a box every key but Escape, and Escape hands the keyboard back, so Alt+2 then
switches. Nothing was changed. The selfcheck now holds this to the source: cmbClass_Click's lack of a focus move, the
ComboBox branch of Form_KeyDown, both ports' Spell Type handlers (which only list the class), the Escape in both, and
Alt+digit from a box going to fn15DF only on a service form. The audit found no other control with the same problem.
The character windows (FRMCHAR1.BAS:39-53, FRMCHAR4.BAS:17-33) and the Party window (FRMGROUP.BAS:51-66) give every
key to fn1521 and have no combo or text box. The port's character pages scroll in a ScrollViewer whose bars take no
focus when clicked, as the same headless run showed. Making a pick give the keyboard back would be a change to the
original. If it is wanted, it fits as a Quality of Life toggle.

## Sound effects: WaveMix's eight channels (2026-10-21)

**Sound effects are mixed, as the original mixes them - replaces the deviation "Sound effects are not mixed".** The
port played every WAV through winmm's `PlaySound`, which plays one sound at a time, so each new sound cut off whatever
was playing. The original plays them through WAVMIX16.DLL (MODULE13.BAS:7-18). `fn1A08` (MUSIC.BAS:376-399) configures
a session mono at 22 kHz, opens all eight channels (`WaveMixOpenChannel(gv20C8, 0, 1)`, WMIX_OPENALL) and activates it.
The port now does the same thing in software: `WaveMixChannels` (Mordor.Core/WaveMix.cs) is eight queues of waves summed
into one stream, with clipping and no scaling, at 22,050 Hz; `WaveOutStream` (Mordor.App) plays that stream through
winmm's `waveOut` in buffers of about 17 ms, four ahead. No library was added. Every WAV the game ships is 8-bit mono 22,050 Hz,
WaveMix's own format, so nothing is resampled.

`WaveMixSession` is MUSIC.BAS's bookkeeping, line for line:

- **sub09DE** (MUSIC.BAS:136-169), the routine nearly every effect goes through, flushes the sound's channel and then
  queues the wave on it (`WaveMixFlushChannel`, then `WaveMixPlay` with flags `mc0134 And mc0136` = 0, WMIX_QUEUEWAVE).
  So a sound cuts off its own last play and mixes with every other sound.
- Its channel comes from **fn19F4** (MUSIC.BAS:348-374): channels 1 to 7 in turn, passing over one that another open
  sound was handed by this path in the last five seconds, for at most eight tries. A sound keeps its channel for five
  seconds from when it was *given* it, not from its last play (`M4EE6` is stamped only when a channel is handed out,
  in sub1A31 (MUSIC.BAS:53, 73)). After that its next play takes a channel afresh.
- **sub086D** (MUSIC.BAS:200-229) takes a new channel from fn19F4 on *every* call and queues there without a flush, so
  two plays of the same sound mix. It stamps no time, so it holds no channel against anyone.
- **sub08D1** (MUSIC.BAS:231-248) passes sub1A31 a mode that is never assigned (`m00C4`, 0), which puts the sound on
  `gv2162`, the channel fn19F4 handed out last. Its flags are another never-assigned Static (`m0166`), so it queues too.
  The only caller is the fall, `sub086D 6: sub08D1 2` (MAZEMOD.BAS:1762, 1868), so the HIT plays after the FALL, on the
  FALL's channel.
- **sub1A31** (MUSIC.BAS:30-77) opens a sound's wave the first time it is played. The list of open waves is
  `ReDim gv2130(0)` (MODULE1.BAS:307) and is used from index 1, and once the top index is 7, **sub1A3F**
  (MUSIC.BAS:250-261) makes room. So at most seven waves are open, not eight. sub1A3F frees `gv2130(1)`, the wave opened
  longest ago, however recently it was played. A sound whose file is missing has its channel set to 0 (MUSIC.BAS:36),
  and nothing else ever reaches channel 0. FIGHT.WAV, sound 0, is not shipped.
- `Timer` is VB's seconds since midnight, a Single (`M4EE6 As Single`, MAIN.TXT:811). The port uses the same clock, so
  the five seconds also run out at midnight.

Which routine each port call site uses is its original call site's. The sub086D calls are: the credits' random sound
(FRMCREDI.BAS:92); MAKELEV (FRMCTYG.BAS:416); the dungeon's SCURRY, MOAN or DSTSLAM (FRMDUNM.BAS:447); DROWN (506); FOG
and WATER or WATER1 (590-593); RAISE and POISON in the morgue (FRMMORG.BAS:96-100) and on a raise (MAINMOD2.BAS:349, 1652,
3100); WITHER on the trip to the sanctuary (1350); DIE in mdrcharSetDead (4558); TELEPORT, both the spell (5815) and the
square (MAZEMOD.BAS:1789); an item's MAGIC (MAINMOD2.BAS:6160); WADE (MAZEMOD.BAS:1705); the extinguisher's EXTING
(1712); the fall's FALL (1762, 1868); and the EXTING of an item that attaches itself (MONSMOD.BAS:3857). The fall's HIT is
sub08D1. Everything else is sub09DE: every sound of a fight, a spell's sound (sub1005), the cry for help, the Messages
window's ding, BETTER, SLIMETR, YEECH, HELLO, OUCH, FOUND and ASMO. The selfcheck reads every call of the three routines
in the decompiled source and holds the App to it. No App sub09DE call may play a sound the original plays only through
sub086D. Every App sub086D call must be one the original makes through sub086D. The fall must be sub086D then sub08D1.
It also runs the bookkeeping on a fake WaveMix (channels, the hold, the eighth wave, a missing file) and mixes real
samples to show flush against queue.

What stays as it was. No Sound FX, `MORDOR_MUTE=1`, No Ding and No HELP Sound stop a sound before any of this
(MUSIC.BAS:143, 204, 235). While the game is minimised, `WaveMixActivate(gv20C8, False)` (FRMMAIN.BAS:211) is the
channels being emptied and the device closed; the next effect after coming back opens it again. Music is untouched.

**The FX volume works (2026-10-21) - replaces "the FX Vol. slider is stored but drives nothing".** `gv219A` is a Long in
`waveOutSetVolume`'s own form: left channel in the low word, right in the high, 0 to &HFFFF. sub09DE (MUSIC.BAS:157-164)
and sub086D (218-225) both run the same lines after the wave is found open and before it plays. If `gv219A` is not 0,
they read the device's volume and set it to `gv219A` if it differs (`waveOutSetVolume(0, gv219A)`). If it is 0, they
only read it. Nothing puts the volume back, so it stays at the FX setting until the next play. sub08D1 has no such
lines. The port does the same in `WaveMixSession` (`FxVolume`, `WaveOutSetVolume`). The device it sets is the port's
own output: `WaveMixChannels.DeviceVolume` scales the mix after the channels are summed and clipped. So a change
reaches what is already sounding, as a device volume does. The output is mono, so only the low word counts. Windows'
own volume is never touched, and the music is MIDI on another device, as the original's was, so it is not affected.

The setting:
- MORDOR.INI's `FXVolume` is `Str(gv219A)` (FRMOPTIO.BAS:242). A missing key reads as "-65538" (MODULE1.BAS:983-987):
  &HFFFEFFFE, the slider at its top.
- The slider is 0 to 32767. Moving it takes `Hex(Value * 2)` to four digits and doubles the digits into a Long with
  `CLng("&H" + h + h)` (FRMOPTIO.BAS:396-412).
- The new value takes effect at once and plays sound 2, HIT, through sub09DE, so you hear the volume you chose. Cancel
  puts back the value the form loaded with (FRMOPTIO.BAS:148). The port does the same if the window is closed any
  other way without OK.
- Form_Load shows `(gv219A And &HFFFF&) / 2` (354-356).
- The port's settings.ini kept a 0 to 100 slider position here before. Nothing read it. A 0 from then still means
  "leave the volume alone", which is full.

Not known, and chosen:
- The slider's Max is in the form's binary. 32767 is VB's ceiling, and it fits the default &HFFFE.
- At `gv219A` = 0 the slider keeps its design-time value, which the decompile does not give. The port shows 0.
- `waveOutGetVolume` is declared `ByVal p2 As Any` and handed a Long, so it is given a null pointer, not the
  variable's address. Either it fails or it writes nothing useful. The port reads nothing back.
- The original's device started at whatever Windows' wave volume was. The port's output starts at full.

**No Sound FX is live in the Options window (2026-10-21).** The port used to apply the box only on OK. The original
applies it as soon as it is clicked (chkCheck_Click, Case 2, FRMOPTIO.BAS:88-99), and so does the port now.
- **Ticked:** `gv1A66.M5EFC` is set at once and the FX slider greys out. Then `mdrDeInitializeFX`
  (MUSIC.BAS:268-289) runs: every open wave is freed, its `M7208` becomes the free's 0, and the session is closed.
  The port does the same with `WaveMixSession.DeInitialize` and `WaveMixChannels.CloseSession`, and gives back its
  wave output.
- **Cleared:** the flag goes off at once and the slider comes back. Then `fn1A08` opens a session again
  (`WaveMixChannels.OpenSession`, and the output is opened there and then). `sub09DE 11` plays RAISE. If the device
  will not open, the form's "Error initializing Sound FX!" box ("Sound FX") comes up and the slider greys out again.
- **Cancel** puts back only the flag, `gv1A66.M5EFC = m0056` (FRMOPTIO.BAS:140). It neither opens nor closes the
  session.
  - So if you tick the box and then Cancel, effects are switched on, but over a closed session. `gv20C8` still holds
    the closed handle, so no wave opens and the game stays silent until the box is cleared again.
  - The port reproduces this: a closed session opens no wave.
  - Whether WAVMIX16 itself refuses a closed handle is not in the source. Its handle is freed by
    `WaveMixCloseSession`, so this is the reading taken.
- **What is chosen:** closing the window other than by OK counts as Cancel.
- The port made its session on the first effect. Ticking the box now makes the session first if there is none, so
  that closing it means what it meant in the original, which opened it at start-up.
- `MORDOR_MUTE=1` now holds from the first touch of `Audio`, not only from `Audio.Init`. The title screen's Options
  window can play an effect before any `Init`.

Not known, and chosen:
- Whether `WaveMixFreeWave` stops a wave that is still playing. The port takes a freed wave off every channel. That
  is safe, and only matters when an eighth distinct sound comes while the oldest-opened one is still sounding.
- WaveMix's own output latency (its buffers, and the remix it does only with WMIX_HIPRIORITY, which is never passed).
  The port starts a sound within about 67 ms plus what Windows adds.
- WaveMix sums 8-bit samples. The port sums them in 16 bits and clips at the 16-bit limits, which for 8-bit sources is
  the same clipping.

**Three port sounds with no source, removed (2026-10-21).** Every call of sub09DE, sub086D and sub08D1 in the
decompiled source, resolved through its module's Consts, plays FOG (65), RAISE (11) and HELLO (62) only here:
- FOG: only the ailment clock's, a quarter of the time while standing in fog (FRMDUNM.BAS:589-591). The port also
  played it on stepping into a fog square; MAZEMOD.BAS's arrival code has no sound for fog, so that one is gone.
- RAISE: the morgue's companion raise (FRMMORG.BAS:100) and a raise being taken up (MAINMOD2.BAS:349, 3100). A companion
  raised by spell (MAINMOD2.BAS:1072-1087) says "Raise Was Successful!" in silence; the port's RAISE there is gone.
- HELLO: only for strangers on the dungeon square (MAZEMOD.BAS:371-372). FRMCTYM.BAS plays no sound at all, so the
  town's HELLO on arrival is gone.

The selfcheck holds each: one FOG call in the App, the clock's; HELLO only in the Dungeon window; nothing played in the
spell's companion branch.


## Gold is VB Currency (2026-10-21)

The character record keeps `Gold` and `BankGold` as **Currency** (MAIN.TXT:332, 335). Currency is a 64-bit integer
counting ten-thousandths: four decimal places, exact sums, a range of ±922,337,203,685,477.5807. The port had kept
whole gold in a `long`. It now keeps a `VbCurrency` (Mordor.Core/VbCurrency.cs) that behaves as VB's does:
- Currency with an Integer, Long or Currency is exact.
- Currency with a Single or Double is worked in Double. Storing the result in a Currency rounds it to four places,
  half to even.
- A Currency stored in an Integer or Long, or put through `CLng`, rounds half to even.
- Going past the range is error 6, Overflow.

**The other Currency fields.**
- `Fight.Gold` (T4F0B, MAIN.TXT:105) was already a decimal rounded to four places. It now goes through the same
  conversion: the Double times 10,000, rounded half to even. Before, `(decimal)d` cut the Double to fifteen
  significant digits first.
- A Hall of Records value, `M73F5` (MAIN.TXT:846), is Currency for every record. Only the Wealthiest can carry a
  fraction, and the port's record now keeps it.
- `M579B`, experience, is Currency in the record and in each guild membership (MAIN.TXT:280, 331). Experience comes in
  through `sub0F11`'s `p0098 As Long` (MAINMOD2.BAS:279), so it is always whole in play. Only the character editor
  writes it another way, with `CLng`. The port keeps it as a `long`, unchanged.
- `M5906(8)` holds kill and quest counts, play time and a date. `CombatOrders.M68C8` holds gold recovered from a
  thief, added from the Long `l04DE`. `T5BF1.M5C00` and `T5D20.M5D2C` are counts and wall bits. None of them is gold
  that can carry a fraction. `BitMasks` is bit masks.
- An item's `Price` is a **Long** (MAIN.TXT:236), as are the price functions. That covers `sellPrice`, `fn12B2`
  (the guild's fee), the morgue's `l04CC`, and the Confinement's `fn33FA`, which is declared Currency but returns a
  Long `l024A`.

**Where a fraction can come from.** Only one place: the Bank's text boxes. `txtAmount_KeyPress` reads
`l00E6 = CCur(txtAmount(Index).Text)` into a Currency (FRMCITYB.BAS:304-308). Type "10.5" and both balances carry
the half from then on. Every other way gold comes in is whole:
- a fight's share is `CLng(l039C / n)` (MONSMOD.BAS:2152);
- a sale is a Long;
- Give and the Exchange read `CLng` of the typed amount (MAINMOD2.BAS:3716, FRMEXCHA.BAS:108);
- a thief's take is a Long.

Once a fraction is there, each flow handles it as the original does:
- **The Bank** (`Party.BankTyped`): the amount is CCur of the text, kept to four places half to even, with the
  locale's separators, exponents and `&H`. An amount that is nought at four places ("0.00004"), or text that is not
  a number, does nothing and leaves the box as it is (`If l00E6 Then`, On Error Resume Next). The "All" buttons fill
  the box with `LTrim$(Str$(balance))`: "89.5", or ".25" with no leading zero (FRMCITYB.BAS:29-31). The Party
  Options sum exactly.
- **charPay** (MAINMOD2.BAS:693-708, `Character.Pay`): the charge is a Long. When the bank makes up the difference,
  `p0196 = p0196 - Gold` puts a Currency back into that Long, so the bank's part is rounded half to even. 100 owed
  with 50.5 on hand takes 50 from the bank, and an account of 49.5 is left at -0.5. The port's early return for a
  charge of nought or less is gone, because charPay has none.
- **A thief** (MONSMOD.BAS:3337-3351, `CombatEngine.GoldToSteal`): when the roll is more than the purse, `l04DE`
  takes all of it as a Long. On 10.5 that is 10, and the half stays. On 11.5 it is 12, and the -0.5 left is set
  back to nought.
- **Give**: `Val(answer) > Gold` compares a Double with the Currency, so "10.5" passes against 10.5. Then `CLng`
  makes the amount 10, and the half stays.
- **Exchange**: a whole amount is taken from a purse with a fraction, and any shortfall comes out of the bank exactly.
- **Alt+P, the Bank's pools, the guild's purse tests** (`Gold + BankGold < fn12B2`), **the morgue's** and **the
  Seer's**: all exact.
- **The Wealthiest record** (HALL.BAS:470): `pv0104 > M73F5` compares exactly, so a bank of 1,000.5 takes a record
  of 1,000, and the record keeps the half.

**What the screen shows.** Every gold figure in the original goes through
`formatNumber(CCur(x))`, which is `Format$(x, "###,###,###,###,###")`, with "0" for nought (MODULE1.BAS:194-213).
There are no decimal places, so a fraction is never seen. Format$ rounds a half **away from zero**, unlike `CLng`:
10.5 shows as "11", and -10.5 as "-11". A negative value that rounds to nothing keeps its sign: -0.4 shows as "-",
as `Format$(-.001, "0.00")` is "-0.00". The port's `CharStatFile.FormatNumber(VbCurrency)` does this. It is used by
the character panel, the toolbar, the Bank's status line, the guild's Info box, the Exchange panels, the Hall, the
guild log's record line, and the theft and Give lines. Two figures that had been wrong at a half are now right:
- **A fight's share line** quotes `formatNumber(CCur(l039C / n))` (MONSMOD.BAS:2161, 2167), while the share paid is
  `CLng` of the same quotient. 5 gold between two pays 2 each, half to even, and says "Your share is 3 Gold!". The
  port had quoted the 2 it paid.
- **The Hall's stat and skill records** are `CStr` of the Currency (FRMHALL.BAS:131-272). They are whole in play.

**Saves.** A character's file keeps `"GoldOnHand"` and `"GoldInBank"` as the integers they always were. The
integer is the whole part, with the fraction dropped toward zero. Only a balance with a fraction writes its exact
figure beside it, as `"GoldOnHandExact": 10.5` or `"GoldInBankExact"`. A Hall record does the same with `"Value"`
and `"ValueExact"`.
- An old save has no exact field and loads unchanged. A real character file read and written back is byte-for-byte
  the same.
- A build from before this skips the field it does not know and reads the whole part. It loses the fraction and
  nothing else. A save it writes has no exact field, so no stale figure is left to be believed later.
- An exact figure whose whole part does not match the integer (an integer edited by hand) is ignored, and the integer
  is used.

**Mods.**
- Auto-deposit (`Party.PartyDeposit`) and Alt+P pool exactly.
- "Party covers shortfalls" hands over exactly the shortfall, fraction included. Its line shows the amount as
  formatNumber would: 0.5 is "1".
- The mod API's `ShortfallRequest.Shortfall`, `ShortfallGift.Amount`, `TownArrivalAnswer.Message`'s argument and
  `ModHost.ArrivedInTown`'s total are now `VbCurrency`.

The selfcheck's "gold is Currency" section checks all of the following: the types and lines in the decompiled
source, the type's arithmetic, rounding, range and Str$, formatNumber, and every flow above with a fractional case.
It also round-trips a save, reads it as an older build would, and loads an old save. The mods probe adds the
fractional cases for auto-deposit and party cover.

Not known, and chosen:
- **CCur of a string.** The port parses the text exactly, as a decimal, then rounds half to even at four places.
  VB3's runtime may convert through a Double first. The two can differ only on a typed amount with five or more
  decimal places that lies at a tie, such as "1.00005".
- **Format$ of a negative value that rounds to nothing.** "-" is taken from VB's documented `Format$` behaviour.
  The only way to reach it is a balance between -0.5 and 0, left by a negative withdrawal or by charPay's rounding.
- **Str$ always writes "."**, but CCur reads the locale's decimal point. Where the locale's point is a comma, the
  original's "All" on a balance with a fraction would misread its own text. The port does the same, and the
  selfcheck's All-button round trip runs only where the point is ".".

**Overflow.** A balance can reach the edge of Currency's range. Typing "-900000000000000" into Withdraw does it,
because a negative withdrawal is not refused. In VB, Overflow is run-time error 6, raised at the statement. What
happens next depends on that routine's own error handling. The port never dies of it. Each call site follows its
original's handling, or, where the original has none, refuses safely:
- **On Error Resume Next skips only the overflowing statement.** This applies to the Bank's text box
  (FRMCITYB.BAS:305). A deposit into a full account leaves the account as it was and still takes the gold off the
  hand. A negative withdrawal that would carry the account past the top still debits the hand. That gold is lost, as
  it is in the original.
- **A trap leaves the routine where it got to.** Its line goes to `sub04FC`, which the port never shows, so this is
  silent.
  - Alt+P (fn1521, MAINMOD2.BAS:3383): the members before the overflow are pooled, and the rest keep their gold.
  - Give (sub14B5 under fn1521's trap): nothing more happens, and no line is shown.
  - The Exchange (FRMEXCHA.BAS:97, 128): the transfer stops at the overflowing statement. `Gold + BankGold` in the
    "Too Much!" test is formed on both levels, because VB's `Or` evaluates both sides.
  - A fight's share (monGetGold, MONSMOD.BAS:2133, 2184): the members before are paid, and no line is shown.
  - A thief (sub0E85, MONSMOD.BAS:2669, 3414; its handler calls it "monMonsterAttack"): `Gold - l04DE` overflowing ends that group's attack.
- **A trap that resumes skips the statement and shows its line.** This is fn0D4D, which generates the chest gold
  (MONSMOD.BAS:1426, 1984-1986). The skipped assignment leaves the gold as it was, and Messages shows "Base Error
  'Overflow' Generating Monsters!". The shipped MDATA5 cannot reach it (its gold factors run 2 to 7); only edited data
  can.
- **No handler would end the program.** The original would end before anything was saved. The port leaves every
  balance it would have touched as it was. This applies to:
  - the Bank's three Party Options (FRMCITYB.BAS:38-59);
  - charPay, which refuses the charge;
  - a Store sale (FRMCTYS.BAS:240) and a Confinement sale (FRMCITYC.BAS:284), which do not happen;
  - the Quality of Life mod's auto-deposit and shortfall cover.
- **A figure only compared or shown is held to the range.** The purse (`Gold + BankGold`) is used this way in the
  toolbar, the guild's Info, the Exchange panels, the character file, the guild and Seer purse tests, and the mod's
  shortfall. Against any amount inside the range, the held figure answers as the true sum would. When the true sum
  is past the range, the display shows the edge of the range rather than the original's error.

Loading never throws for any of this, and the safeguards apply to a character file and the Hall alike.
- A whole part past Currency's range is held to the range.
- A whole part past a 64-bit integer is read as a rough number and then held to the range.
- An exact figure that is not a number a decimal can hold is ignored, and the whole part is used.
So the character still loads, and the Hall of Records file is read rather than replaced by a fresh one that the
next save would write over the old.


## The game folder, and the shareware played as the limited build (2026-10-22)

A port necessity, not a rule of the game: the original found its data through `[File Information]
BasePath` in MORDOR.INI and, when that was wrong, said "Error reading ...! Is BasePath in MORDOR.INI
correct?" and ended (MAINMOD.BAS:1193-1196). The port reads the player's own installed copy and
ships none of the game's data, so without that copy there is nothing to play; when the files it
needs are missing or do not read, a first-run window asks for the folder. No code check stands
between a player and the game beyond that: the full-version gate first written here (refusing the
shareware) was taken out the same day.

**The shareware is played as the limited build.** The original has no run-time shareware test -
its limited MORDOR.EXE is the same source compiled with a few constants changed - so the port goes
by the data: fewer than fifteen levels in MDATA11, or monsters or items that stop short of the
fifteenth, is the shareware ("limited to 3 dungeon levels", MORDOR.WRI). With it, the limited
build's branches are taken (`Mordor.Core/LimitedVersion.cs`):

| the full build's constant | the limited build | port |
|---|---|---|
| `m009C = 0` (FRMMAIN.BAS:34) | caption "Mordor 1.1 (Limited Version)" (148-152); no goodbye box (240-248) | city and dungeon frames; `OriginalBox.ThankYou` |
| `m0622 = 0`, `mc0624 = 15` (MAINMOD2.BAS:224-225) | TAKE on a chute on level 3: "You cannot take this Chute in the Limited Version." (3997-3998); the "Public Version" box from Sub Main (6455, sub10F8 6339-6345) | DungeonWindow's TAKE; once a run over the menu |
| `mc015E = 15` (MAZEMOD.BAS:26, fn0A0B 409-422) | any level past 3: "LIMITED VERSION: Request to go to Level n is Denied.", and level 3 instead | `ChangeLevel` (stairs, teleporters, Sanctuary), each chute landing (sub08F5, MAZEMOD.BAS:114-115) and the Teleport spell before its tests (MAINMOD2.BAS:5823) |
| `m02A6`, a Static never set (MAZEMOD.BAS:1535) | a teleporter to past level 3: "..but it doesn't go anywhere in the limited version..." (1794-1796); a chute walked onto on level 3: "...doesn't do anything in the Limited Version." (1840-1841) | the square routine |
| `m0032`, never set (FRMABOUT.BAS:8, 24) | About's "Ordering Info" button (FRMABOUT.FRM: 90,1080 1425x285) opening frmOrderInfo (FRMORDER.BAS/.FRM) | `AboutWindow`, `OrderInfoWindow` |

Values that are inference, and why: the limited build's `mc015E` and `mc0624` are taken as 3, the
shareware's depth in MORDOR.WRI - the full build's 15 is its deepest level, and the shareware's
deepest is 3. `m02A6` and `m0032` are variables, not constants, in the full build's decompile and
nothing assigns them; their messages say "limited version" and the button opens the order form, so
they are taken as on in the limited build. Not switched: the "PUBLIC BETA" notice that writes
`Limited=True` to MORDOR.INI (MODULE1.BAS:932-940) hangs on `gv0180`, a beta flag whose text speaks
of `gc0182` = 15 levels - not the shareware's; and frmWelcome's "Limited " (FRMWELCO.BAS:14-16), a
splash the port does not have. Details in `docs/GAME_FILES.md`.

Saves: a folder of the player's own (`%APPDATA%\OpenDejenol\saves`) unless `saves` beside the game
folder already holds something, which is then kept as it always was; `MORDOR_SAVES` before both.
