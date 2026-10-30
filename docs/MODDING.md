# Modding

## Status (2026-09-27)

Built: the loader, the mod context, the Mods page (the main menu's and the Control Panel's **Mods...** buttons), save tagging, the safeguards, Harmony, the first hooks,
and the first mod.

| Part | State |
|---|---|
| Loader (`mods/*/mod.json`, one AssemblyLoadContext per mod, errors logged, a throwing mod disabled for the session) | built |
| Mods page (the main menu's **Mods...** button, and in a game the Control Panel's **Mods...** beside Game Options; until 2026-10-04 it was inside Options, which now has only OK and Cancel, as bm3.png): enable per mod, each mod's toggles as checkboxes, kept in settings.ini; since 2026-10-25 no description is written on the page - a mod's description is its heading's tooltip and each setting's its control's - and a mod may ask for a settings window of its own instead (a **Settings...** button) | built |
| Save tagging and the "saved with mods that are not running" box | built |
| Harmony (`ctx.Harmony`, id = mod id; every patch logged, removed if the mod throws) | built |
| Events: `MessageLogged`, `CharacterSwitched`, `StoreRowClicked`, `CaptionShown`, `ArrivedInTown`, `StoreShelved` | built |
| Decisions: `StoreAllowsRowChangeWhileChoosing`, `CityAltP`, `MessagesKept`, `TownArrival`, `StoreRowVisible`, `CoverShortfall`, `StoreRowOrder`, `TownIncomeBank` | built |
| UI slots: toolbar tooltips, Objects & Spells commands, pack-line marks (`PackRowDecoration`), Bank-line marks (`BankRowDecoration`), Bank commands (`AddBankCommand`), Party Information columns (`AddPartyColumn`, the PartyRowExtras idea), Store check boxes (`AddStoreOption`), Store Buy-list columns (`AddStoreColumn`), sortable Store headers (`StoreHeaderSort`, `StoreHeaderClicked`), `AddWindow` | built (`AddWindow` windows open from a toolbar button, and remember open/closed and place) |
| Actions (awaited moments a mod acts through the game's own commands): `EnteredDungeonFromTown`, `LeavingByStairs`, with a `DungeonContext` (the party, the tables, `Cast`, `UseItem`) | built (2026-10-04, for the Kaizen mod) |
| List settings (mod.json `tables`: rows of text cells, edited on the Mods page, `ModSettings.GetRows`) | built (2026-10-04); no shipped mod declares one since Kaizen 1.1.0 |
| `ctx.Game` (the party in play, the one in command, the spell and item tables, `Changed`), `ctx.DataDirectory` (the saves folder, for a mod's own files), the event `CharacterDeleted` and `ModSettings.Changed` - API version 3 | built (2026-10-04, for the Kaizen window) |
| `ModGame.AskCastOnWho` (the game's own "Cast on Who?" box for a mod's window) and `ModDragged` (the Spells-list and pack drags the buffers take, readable by a mod's drop target) - API version 4 | built (2026-10-08, for the Kaizen window's drag and drop) |
| The event `DamageDealt` (hits a party member or one of their companions takes off a monster, and each kill: `CombatDamage`, raised by the combat engine) - API version 5 | built (2026-10-21, for the DPS Meter mod) |
| The event `ExperienceGained` (experience put on a party member's total by sub0F11, once per award, with what the total actually went up by and its source - damage dealt or a chest's trap disarmed: `CombatExperience`, raised by the combat engine) - API version 8 | built (2026-10-25) |
| `ModSettings.Has(key)` and `ModSettings.Remove(key)`: whether settings.ini holds a value, and forgetting an old key, for a mod that merges or drops settings (the Quality of Life mod 1.8.0) - API version 8 | built (2026-10-25) |
| Gold as `VbCurrency` (VB Currency, four places): the `TownArrival` deposit total, `ShortfallRequest.Shortfall` and `Party.ShortfallGift.Amount` - API version 6. `ModApi.ApiVersion` is a constant a mod may read; the loader does not check it, and a manifest names no API version, so nothing refuses a mod built against an older one: a mod built against version 5 that touches those three must be rebuilt. The mods shipped here are built with the game, so the Quality of Life mod keeps its version (1.7.1) and saves tagged with it load without a warning | built (2026-10-21, gold as Currency) |
| `IModLifecycle` (`OnEnable`, `OnDisable`): a mod switched on and off on the main menu's Mods page, no restart (greyed on the in-game page); `ModHost.Switch`, `ModHost.StatusOf`, `LoadedMod.Active`/`Running`/`SwitchesLive`, `ModSwitchResult`; `CharacterDeleted` reaches switched-off mods too - API version 7 (see "Switching on and off on the main menu"). The three bundled mods implement it and keep their versions | built (2026-10-24) |
| `ModUI.MessageLineColour` (a Messages line's colour, asked once per line with `MessageLineInfo`: the text and its `MessageKind`, which the game gives each line where it makes it - `Party`, `Foes`, `Chest`, `Join` or `None` - never read from the text), `CombatLine.Kind` - API version 9 | built (2026-10-25, for the Quality of Life mod's Color-coded combat messages) |
| Decisions `PartyGoldTo` (who takes the whole of the gold monGetGold pays - a won fight, an opened chest, a room emptied by joining - instead of the even share; `CombatEngine.GoldTo`) and `TownIncomeToBank` (a Store or Confinement sale paid into the bank, `TownIncome`, `Character.Receive`); choice settings (mod.json `choices`, `ModChoiceSpec`, radio buttons on the Mods page, `ModSettings.GetChoice/SetChoice`) - API version 10 | built (2026-10-25, for the Quality of Life mod's Party gold and Bank-managed town gold); both decisions gone in API version 12 (below), the choice settings kept |
| Settings windows (mod.json `settingsWindow`: a title and groups of settings by key, `ModSettingsWindowSpec`; the Mods page shows a **Settings...** button for the mod in place of its settings, on both pages and while the mod is off, one window per mod; `ModHost.MigrateSettings` runs the mod's static `Migrate(ModSettings)` first; `ModSettingsWindow` draws each group with a heading box that switches its toggles together, shown mixed when they differ) - API version 11 | built (2026-10-25, for the Quality of Life mod) |
| `PartyGoldTo` and `CombatEngine.GoldTo` removed (the dungeon's gold is always monGetGold's even share, with any mod); `TownIncomeToBank` replaced by `TownIncomeBank` (the member whose account a sale's gold goes into, or null for the seller's hand; `TownIncome.Party`); `TownArrivalAction.PartyPool` with `TownArrivalAnswer.PoolTo` (the Bank's Party Pool into one member's account, `Party.PoolOnHandToBank`); `ShortfallAnswer.From` (who covers a shortfall, in order) and a shortfall now covered from the givers' bank accounts only; `ModChoiceSpec.OptionDescriptions` (a tooltip per option) - API version 12 | built (2026-10-25, for the Quality of Life mod's Town gold) |
| The Automap hooks (see "The Automap" under **UI** below): `ModUI.AutomapMenu` (items for the right-click menu, whose first item is the original's re-plot), `AutomapMarks` (tints, frames and arrows drawn on the map's own squares), `AutomapHover`, `AutomapClick`; `AutomapView` (`ShownLevel`, `DeepestLevel`, `LevelSize`, `ShowLevel`, `Refresh`, `PickSquare`, `CancelPick`, `Picking`), `AutomapSquare`, `AutomapMark`, `AutomapMenuItem`; `ModHost.AutomapHooked`; `AutomapLayout.SquareAt` - API version 13 | built (2026-10-26, for the Cartographer mod) |
| The Action `CombatRound` (each tick of an engaged fight, before any character acts: synchronous, not awaited) with a `CombatRoundContext` - a token for the fight (never the engine), the monster list as shown (`MonsterGroupView`: slot, fn0E23's name for the one in command, the count only where the list shows one, the name for one; no hit points), each member's order (`CombatOrderView`, with `OpeningChest`) and aim, and the commands a player gives between ticks for a named member: `Order` (f, d, space), `Cast` (a spell made the order, as a spell buffer fires it), `UseItem` (an item at All made the order), `Aim` (a number key 1-4); `ModHost.CombatRound` - API version 14. With no mod hooked no context is made and the round is the original's | built (2026-10-30, for the Auto-combat mod) |
| Events `BlowLanded`, `HitsChanged`, `FightStarted/Ended`; restyling; data and asset mods | not yet |

The Store's alignment choice is back to the original's lock by default; the unlocked choice is in the Quality of Life
mod. With no mod enabled (the default) the game is the original and `tools/check.sh` asserts the hooks' default
answers (the `mods` probe).
The Bank's Deposit and Withdraw buttons, its double-click and its multi-selection are the game's own (a deviation the user
asked for, docs/FIDELITY.md 2026-09-28), there with mods on or off; the mod adds only the marks and Sort to the Bank.

### The Quality of Life mod (`src/Mods/Mordor.Mod.QoL`, id `QoL`)

Version 1.12.0. Off until ticked on the Mods page; once on, all its toggles default on, and each is read live, so ticking one takes
effect at once. Each toggle shows its label and one short line of description. Since 1.8.0 the four Party
Information conveniences - the Next column, the HP and SP bars, the XP bar and the quest marks - are one toggle,
**Enhanced character UI** (`EnhancedCharacterUI`: "Adds a Next column, HP/SP and XP bars, quest marks and
unidentified-item marks.", the identification marks joining it in 1.9.0), and the XP bar always rides in the Next column (the separate XP column is gone); the two Sort buttons
(Objects & Spells, the Bank) are always there with the mod on (a button the player chooses to press); and the
Store's hiding is set only by its own **Hide unusable** box, its state kept under `HideUnusableInStore` as before.
A settings.ini from before is brought up to it once as the mod loads (`QolMod.Migrate`): Enhanced character UI on
if any of the four was on (one never set counts as its default, on) unless it is set already, then `XpToNextColumn`,
`HpSpBars`, `XpBar`, `MarkActiveQuests`, `ItemSort` and `BankSort` are taken out (`ModSettings.Has` and `Remove`,
API version 8).

1.9.0: the Store's unlocked alignment choice and the toolbar tooltips are no longer toggles - they are simply on
while the mod is; the Store's slot and worn columns, its sortable columns and the shelving line are one toggle,
**Enhanced store UI** (`EnhancedStoreUI`: "Adds Slot and Worn columns, sortable headings, and a note of what a sale
stocks to the Store."); Mark items not fully identified is part of Enhanced character UI; and the Extended combat
log is gone (its window, its toolbar button, the Messages window's 200 lines), replaced by **Color-coded combat
messages** (`ColorCodedCombatMessages`: "Colours combat lines in the Messages window by kind."). `QolMod.Migrate`
brings a settings.ini up once: Enhanced character UI from the five it now covers (`MarkUnidentified` with the
four), Enhanced store UI from `StoreSlotColumns`, `StoreSortable` and `SayStoreShelved`, and Color-coded combat
messages from `ExtendedCombatLog`, each on if any old key was on or never set, unless the new key is set already;
then those keys, `UnlockedStoreAlignment`, `ToolbarTooltips` and the Combat Log window's `Window.CombatLog.Place`
and `Window.CombatLog.Open` are taken out. The save tag names the mod as `QoL@1.9.0`; a save's missing-mods check
compares ids only, so a save tagged by an older QoL is not reported as missing it.

1.10.0, gold: **Alt+P pools and deposits everything** is gone - ALT-p is the game's own on-hand pool (fn1521) again,
with the mod on or off. A choice setting, **Party gold** (`PartyGold`: "Who gets the gold the party wins in the
dungeon."), with two options: **Split evenly** (the default) is the game's own share, monGetGold's, untouched (the
mod answers nothing); **All to the leader** gives the whole of every such payment - a won fight's gold, a chest's
(monOpenChest pays through monGetGold, MONSMOD.BAS:3660), a room emptied by monsters joining, and any gold recovered
from a thief, all of which go through `CombatEngine.PayGold` and nothing else in the dungeon pays the party - to the
party's leader, the first member; when the leader is dead, to the next living member in party order (the first
member if none is alive). When that member's purse cannot hold the whole pot (Currency's limit), the game's own
share is paid instead, as Split evenly would. The leader watching sees the game's "Your share is N Gold!" with the whole amount (and
"... and you recovered M Gold!" as the game words a recovery); anyone else watching sees no share line, since they
got none. No roll is made or skipped, so the fight is otherwise the same. **Bank-managed town gold**
(`BankManagedTownGold`: "In town, gold goes into the bank and charges are paid from it, with the party covering
shortfalls.") replaces and merges Auto-deposit gold in town and Party covers gold shortfalls: walking into town
deposits everyone's gold on hand (as Auto-deposit did); gold from a sale - the Store's Sell, the Confinement's Sell,
the town's only payments to a character - goes into the seller's bank account instead of their hand
(`TownIncomeToBank`, `Character.Receive`, with the game's overflow refusal for the account); charges are taken from
the hand, then the bank, as the game's own charPay always does; and the party covers a shortfall as before.
`QolMod.Migrate`: Bank-managed town gold on if either old key was on or never set, unless set already; then
`AutoDepositInTown`, `PartyCoversShortfall` and `AltPPoolAndDeposit` are taken out.

1.11.0, the settings window (the user's redesign): the Mods page shows no settings for the mod, only a **Settings...**
button that opens the **Quality of Life settings** window (`settingsWindow`; from either page, the mod on or off, and
every box editable in a game). Its five groups are the five above, and inside each the features can be switched one
by one again, each under its own key (the original ones where there was one): **Party gold** (the choice: Split evenly,
All to the leader); **Bank-managed town gold** - Deposit gold on arriving in town (`AutoDepositInTown`), Sale gold goes
to the bank (`SaleGoldToBank`), Party covers shortfalls (`PartyCoversShortfall`); **Enhanced character UI** - Next
column (XP to next level) (`XpToNextColumn`), HP/SP bars (`HpSpBars`), XP bar (`XpBar`, drawn behind the Next
column's figure, so seen with that column only), Quest marks (`MarkActiveQuests`, before the figure, likewise),
Unidentified-item marks (`MarkUnidentified`); **Enhanced store UI** - Slot and Worn columns (`StoreSlotColumns`, with
the pack's slot label), Sortable column headings (`StoreSortable`; Slot sorts only while its column is shown),
Note what a sale stocks (`SayStoreShelved`); **Color-coded combat messages** (`ColorCodedCombatMessages`). Each group's
heading box switches its features together and shows mixed when they differ; every box has its description as a
tooltip. `QolMod.Migrate`: a merged key of 1.8.0 to 1.10.0 (`EnhancedCharacterUI`, `EnhancedStoreUI`,
`BankManagedTownGold`) gives its value to each of its features not set on its own (on: all on; off: all off) and is
taken out; a feature already set keeps its value (a settings.ini from before the merges kept the old keys); the rest
as before (the combat log's keys, the dropped toggles). The Kaizen mod's two toggles
stay on the Mods page, their descriptions now tooltips.

1.12.0: the two features made always-on in 1.9.0 are toggles again, in a new first group of the settings window,
**General** ("The Store's unlocked alignment choice and the toolbar's tooltips.", a heading box like the others):
**Unlocked Store alignment choice** (`UnlockedStoreAlignment`: "Another item can be picked while Select Alignment is
up in the Store."; off, the Store's own lock, FRMCTYS.BAS:981) and **Toolbar tooltips** (`ToolbarTooltips`: "Each
toolbar button names its function and key under the mouse."; off, no tooltips, as the original). Both default on and
keep their old keys. 1.9.0 to 1.11.0 took those keys out of settings.ini, so they are usually absent and read as the
default, on - what those versions did; `QolMod.Migrate` no longer takes them out (a value set, by 1.8.0 or now, is
kept). The save tag names the mod as `QoL@1.12.0`.

1.13.0, Town gold (the user's redesign): the dungeon's gold is always the game's own even share - **Party gold** and
its All to the leader are gone, with the `PartyGoldTo` decision and `CombatEngine.GoldTo` behind it, so monGetGold
(`CombatEngine.PayGold`) is the original's with any mod. **Party gold** and **Bank-managed town gold** become one
group, **Town gold** ("In town, gold kept in the bank for you and charges covered from it. The dungeon's gold is
always split evenly."), holding one choice, **Town gold** (`TownGold`: "Where the party's gold is kept in town, and
who covers a charge the payer is short of."), each option with its own tooltip:
**Off** - the game's own: nothing moves on arriving, a sale pays the seller's hand, a charge is charPay alone (the
payer's hand, then their bank) and nothing covers a shortfall; with this (or the mod off) the town is exactly the
original's. **Everyone's own bank** (the default) - walking into town each member's gold on hand goes into their own
account (the Bank's Party Deposit, "Deposited the party's gold: 3,512"); a sale's gold goes into the seller's own
account; a charge is charPay (hand, then own bank), and what is still short comes, exactly, from the other members'
accounts in party order - member 1 first, the payer skipped. **Leader's bank** - walking into town everyone's gold on
hand is pooled into the leader's account (the Bank's Party Pool, `Party.PoolToBank`; "Deposited the party's gold with
"Lead": 3,512"); a sale's gold goes into the leader's account; a charge is charPay (the payer's hand, then their own
bank), and what is still short comes from the leader's account alone (nothing when the payer is the leader). The leader
is the first member; when the first is dead (or not in the city) the next living member in the city in party order
stands in, asked afresh at each move - so a raised leader is the leader again, and what a stand-in was given stays in
that member's own account. A shortfall is never taken from anyone's gold on hand, nor from a member who is dead or
below ground. Overflow is as before: the arrival deposit or pool is all or nothing (the Bank's rule, nothing moved and
no line when any account would overflow), a sale that would overflow the account it goes to is refused with nothing
moved (the game's own refusal for an overflowing hand), and a cover that would overflow the payer's hand is not made
(charPay's refusal follows). After an arrival that moved gold the Wealthiest record is looked at for the one in
command, as the Bank's status line does. `QolMod.MigrateTownGold`, once, unless `TownGold` is set already: `PartyGold`
at All to the leader (case ignored) is Leader's bank; otherwise any of `AutoDepositInTown`, `SaleGoldToBank`,
`PartyCoversShortfall` on, or never set (each read on its own key, else from 1.10.0's merged `BankManagedTownGold`,
else its default, on), is Everyone's own bank; all three off is Off; then `PartyGold`, those three,
`BankManagedTownGold` and `DepositOnArrival` (never shipped; read only when present) are taken out. A settings.ini
with none of them keeps the default, Everyone's own bank - what the old defaults made. The save tag names the mod as
`QoL@1.13.0`.

| Toggle | Planned mod it covers |
|---|---|
| Unlocked Store alignment choice (always on with the mod 1.9.0 to 1.11.0; a toggle in General since 1.12.0) | 1 (commit 164befa's behaviour, exactly) |
| Alt+P pools and deposits everything (town) (removed in 1.10.0: ALT-p is the game's own again) | 8: in town ALT-p runs the Bank's Party Pool & Deposit (`Party.PoolAndDeposit`, the Bank's own rule) and says so in Messages; a party of one gets the original's refusal; the dungeon's ALT-p is the original's on-hand pool |
| Toolbar tooltips (always on with the mod 1.9.0 to 1.11.0; a toggle in General since 1.12.0) | 4 ("AutoMap (F8)", "Control Panel (F10)": the help's names and keys, from `Toolbar.HelpNames`) |
| Item sort (always there since 1.8.0) | 6: a Sort button in Objects & Spells (town and dungeon): worn classes first, then consumables, then Miscellaneous Object; within each by class, then known type, then name; Hands stays slot 1, empties last, each slot moves whole, buffers follow their items; refused while paralysed, allowed in a fight (Swap Items has no combat test) |
| Enhanced character UI: the Next column | 7: a "Next" column in Party Information with the toolbar's own figure (`Progress.ToolbarNeed`), as 950 / 3.5K / 12K / 2.5M; since 1.3.1, once the level is earned, the toolbar's other figure, what is still needed to the pin, in the same form (2.3K), where it said "Ready" before; "Pinned" at or past the pin (`Character.IsPinned`, so experience left above a lowered pin, where the toolbar's figure goes negative, is "Pinned" too). Rounding: two significant figures, half away from zero, done in whole numbers, so 9,950 is 10K and 99,960 is 100K. 44 pixels wide in the form's units from 1.1.0 (56 before the Party window was drawn to frmParty's 291-pixel form), 50 since 1.7.0 to hold the quest mark before the figure |
| Enhanced character UI: HP and SP bars | 2: an "HP/SP" column (40 px) in Party Information, town and dungeon: two 4-pixel bars, hits over spell points, `CurHits/MaxHits` and `CurSpellPoints/MaxSpellPoints` as the window has them. Hits: green when full, through yellow at half, to red when empty. Spell points (since 1.2.0) a scheme of their own, so the two are told apart at a glance: bright blue (70,150,255) when full, through violet (150,95,235) at half, to a dim purple (120,60,150) when empty. Each channel runs straight between the stops. No spell points at all: an empty, dimmed track. The mouse over the cell gives "Hits: 12/15 (80%)" and the same for Spells; the percentage is rounded down, so 100% means full. The rows are drawn afresh on every blow, spell and rest the window already repaints for, so the bars follow |
| Enhanced character UI: XP bar | 5: drawn behind the Next column's text (translucent fills, so the two share one 44-pixel cell and the row keeps its 13 pixels); the separate "XP" column for Next off is gone with 1.8.0. Since 1.3.1 the bar has two stages, one fill in one colour at a time (the 1.2.0 bar - a blue fill to a tick at the next level, then amber on to the pin - is gone). Stage one, short of the next level: green (50,175,95) from this level's threshold (0%) to the next level's (100%), under the amount still needed for the level. Stage two, once the level is earned (the toolbar's Ready): the fill starts again in a magenta purple (205,70,185) over a bar left full green, from the next level's threshold (0%) to the pin (100%), under the amount still needed to the pin; Pinned (at or past the pin, `Character.IsPinned`) is the whole bar purple under "Pinned". The purple has more red than blue and every stop of the SP bar's ramp (70,150,255 / 150,95,235 / 120,60,150) more blue than red, so the two are not confused; the green is a shade apart from the hits bar's full green (60,200,70). `Bars.Experience` gives both fractions (stage one's is 100% once the level is earned, stage two's 0% until then). The thresholds are the game's own: `Character.ExpForThisLevel()` (`fn12A1` at the level below, nought at level 1), `ExpForNextLevel()` (`fn12A1`) and `ExperienceCap()` (`fn13ED`, one short of the level after next; the pin always lies past the next level). Held to 0 below the level's floor (a drain); neither stage is full short of its end. The tooltip gives both numbers, on two lines since 1.7.1 (one line wrapped in a larger Options font and left "pin" alone): "Level 6: 40% of the way" and "1,200 to next level, 5,400 to pin"; once Ready, "Ready to level 6 - 2,300 more to the pin"; experience left above the pin (a drain lowered the pin beneath it, where the toolbar's own figure goes negative) says how far past the pin it is instead of a negative amount |
| Enhanced character UI: quest marks | New in 1.7.0, on by default: in Party Information's Next column a small bold gold ! (240,190,50) before the figure of a member with a guild quest in their **current** guild (Character.Guild, the GuildsJoined(CurrentGuild) entry every readout of the original uses), e.g. !4.3K or !Pinned; a green tick (90,210,100) instead once the quest is done. The state is the game's own, read by Progress.QuestStateOf (Core, read only): None (M55CB 0, or a kind neither readout knows), Active (M55CB 1 defeat a monster / 2 return an item), Done (M57B2). Only a monster quest reaches Done: sub0CF3 sets M57B2 the moment the creature dies (MAINMOD2.BAS:1626-1635; the help's Quests page: 'A quest for a monster is completed the instant the creature is slain'), and the next Make Level clears it with the rest of the quest (FRMCTYG.BAS:464-466). An item quest is completed only when the guild takes the item at Make Level (FRMCTYG.BAS:257-265, which clears it at once), so it stays ! while the item is carried, as every readout of the game has it. The tooltip gains one line, 'Guild quest: ' and the game's own F5 line for it (Progress.Quest, MAINMOD2.BAS:5183-5207): 'You must defeat one Kobold.', 'You must return with one Long Sword.' or 'Your quest has been completed.' - the name the Guild gave the character, nothing more. The mark sits before the figure over the experience bar (since 1.8.0 the column, the bar and the mark come and go together with Enhanced character UI; the bar-off and Next-off cells are gone). The mark is drawn in the row's own face and size, so the row keeps its 13 pixels. The cells come through PartyColumn.WithTables, which hands the monster and item tables to the cell. Enhanced character UI off, or mods off: no Next column. MORDOR_TEST_QUESTS=1 with MORDOR_TEST=city and MORDOR_TEST_PARTY=1 gives Brannoc a done and Sela an active monster quest (Tester's FORFEIT fixture already has an item quest) |
| Color-coded combat messages (1.9.0, in place of the Extended combat log) | 3: the game's own Messages window, town and dungeon, draws a line in the old Combat Log's colour for its kind: the party's side (a member's spell or other action in a fight, `MessageKind.Party`) light green (144,238,144); the foes' (a blow, breath, drain, theft or death a monster dealt, a member's "Ouch!" or "Aigh!", a companion's mishap on a member, `Foes`) indian red (205,92,92); a chest's lines (`Chest`) orange (255,165,0); monsters joining (`Join`) plum (221,160,221). Every other line - and every line with the toggle off, the mod off or no mod - in the window's own colour. The kind is given where the game makes the line (`CombatEngine.FoeMessage`, the `Kind` of a `CombatLine`, the chest and joining code), never read from the text, and the text is never changed. The colour is decided once, as the line is added (`MessageLineColour`), and kept with the line (`MessageLook`, beside the line in the window's list): a line keeps it when the list scrolls or trims and when the Messages are carried between the town and the dungeon, so a line never changes colour once shown; switching the toggle colours the lines added after it. No window or toolbar button of its own; the Messages window keeps the original's 20 lines |
| Auto-deposit gold in town (part of Bank-managed town gold 1.10.0 to 1.12.0; Town gold's Everyone's own bank since 1.13.0) | New in 1.2.0, on by default: when the party walks back into town from the dungeon (the stairs up out of level 1), every member's gold on hand goes into their own bank account by the Bank's own Party Deposit (`Party.PartyDeposit`, FRMCITYB.BAS:43-47), then the Wealthiest record is looked at as the Bank's status line does (`CheckWealthiest`, sub2884). One Messages line with the total, "Deposited the party's gold: 3,512", and only when something moved. Not after a rescue (carried out dead), not at the game's start, not for Exit to Main Menu. Through the `TownArrival` decision |
| Store: hide items you can't use (the Store's own box only since 1.8.0) | New in 1.2.0, **off** until ticked since it hides stock: the Store's Buy list leaves out the rows the character in command cannot use (the Store's own test, `Character.EquipProblem`, the one that draws a row grey). Redrawn on a character switch (the Store's UPDATE), after a buy or sale, and when a mod setting changes, unless the alignment choice holds the list. When the highlighted row is one it now hides, the chosen item goes as it does when a row sells out (FRMCTYS.BAS:92-98: the Item and Cost boxes emptied, BUY and INFO greyed, and a pending alignment choice withdrawn); and when the hiding changes the list's shape the list keeps the item that was at its top, not the row number, falling back to the number when that item is hidden too. The search looks only through the rows listed, so a hidden item is "Item Not Found!". Through the `StoreRowVisible` decision. It is set in the Store itself: a "Hide unusable" check box on the Buy Items heading's line (the `AddStoreOption` slot, since 1.3.1), the only place since 1.8.0 (the Mods page's toggle is gone); its state is kept under `HideUnusableInStore` and the list is redrawn at once. The box is there only while the mod is running; with mods off the Store's page is the original's |
| Mark items not fully identified (part of Enhanced character UI since 1.9.0) | New in 1.2.0, on by default: on the Items page of Objects & Spells (town and dungeon) a " ?" after an item not fully identified, red when Barely known, orange when Partly, yellow when Mostly; the mouse over it names the level ("Partly identified"). Nothing at All. It shows only the identification level, which the item's own Info already tells; the name is the game's, untouched. Through the `PackRowDecoration` slot. Since 1.6.0 the same mark follows a line of the Bank's Deposit/Withdraw Items list (the `BankRowDecoration` slot), read from the vault slot's own identification level; the slot label is not added there (the list is narrow). The Store's list is of stock (no identification level of the player's), so it is not marked |
| Say what a sale adds to the Store (part of Enhanced store UI since 1.9.0) | New in 1.2.0, on by default: when a sale puts the item on the shelf - the Store's own test, cmdSell_Click (FRMCTYS.BAS:264-267, `StoreStock.ShelvesSale`): not cursed, not the Store's own unaligned floor-0 goods, that alignment's row below the cap of nine - Messages says "The store is now stocking a Long Sword (g)." The item's own name, "an" before a vowel (a, e, i, o, u) and "a" otherwise, and the alignment tag ((g), (n), (e)) only for an aligned item, as itemNameFor adds it. Nothing when the sale shelves nothing. Through the `StoreShelved` event |
| Party covers gold shortfalls (part of Bank-managed town gold 1.10.0 to 1.12.0; replaced in 1.13.0 by Town gold's cover from the accounts, below - the rest of this row is 1.3.0 to 1.12.0's) | New in 1.3.0, on by default: in town, when the character in command is charged for something and their purse - gold on hand and bank together, what charPay takes (on hand first, then the bank; `Character.Pay`) - is short, the rest of the party hands over exactly the shortfall, and the charge then goes through as the original's. The givers: every other member in the city (a party of one, or a payer below ground, gets nothing), first each one's gold on hand in party order, then, if still short, each one's bank in party order (`Party.PlanShortfall` / `Party.CoverShortfall`). The gold lands on the payer's hand, so charPay takes the payer's own purse and the gifts together, all of it. If the whole party together cannot cover it nothing moves and the original's refusal follows ("Not enough gold!" and the rest). One Messages line per giver, the mod's own wording with the names quoted as the pool lines quote them: `"fifi" gives "uncanard" 350 Gold.` The party is saved and the toolbar, the Party window and an open Bank redrawn after the move; balances only go down, so the Wealthiest record is not looked at beyond the Bank's own redraw. Every town charge goes through one place (`CityWindow.CharPay`): the Store's Buy, Identify, Uncurse and Realign, the Guild's Join (the fee for a second guild) and Make Level, the Morgue's Raise Character, Raise Companion, Hire Rescuers and a dead character's own raise when asked to pay (not when the guild offers to pay for a low-level character: that offer is on the character's own purse, as the original's), the Seer's three Locates, and the Confinement's Bind, Identify, Realign and Buy Companion. The town's tests of the purse ahead of a charge (Make Level's button and refusal, Locate Character's refusal) ask the same question without moving anything (`CityWindow.CanPay`). Through the `CoverShortfall` decision |
| Town gold (a choice, 1.13.0) | Off: the game's own town, nothing moved for you. Everyone's own bank (default): arriving deposits each member's gold on hand into their own account (`TownArrivalAction.PartyDeposit`); a sale - the Store's Sell (cmdSell_Click, FRMCTYS.BAS:240) or the Confinement's (FRMCITYC.BAS:284) - pays the seller's own account (`TownIncomeBank`, `Character.Receive`); a charge is charPay and a shortfall comes from the other members' accounts in party order (`ShortfallAnswer.From`, `Party.CoverShortfall`). Leader's bank: arriving pools everyone's gold on hand into the leader's account (`TownArrivalAction.PartyPool`, `Party.PoolToBank`); a sale pays the leader's account; a shortfall after the payer's own hand and bank comes from the leader's account alone. The leader: the first member, else the next living one in the city (`QolMod.TownLeader`). Overflow refused all or nothing, as the Bank and the Store refuse it. One line per giver, `"fifi" gives "uncanard" 350 Gold.` |
| Bank-managed town gold: sale gold to the bank (1.10.0 to 1.12.0; Town gold since 1.13.0) | New in 1.10.0, on by default: in town, the gold the Store's Sell or the Confinement's Sell paid the character in command went into their bank account, not their hand; a sale that would overflow the account was refused |
| Party gold (a choice; removed in 1.13.0: the dungeon's gold is always the game's even share) | New in 1.10.0: Split evenly (default) - the game's own even share, untouched; All to the leader - the whole of every dungeon payment (fight, chest, emptied room, recovered gold) to the first member, or the next living one when the leader is dead |
| Store: slot and worn columns (part of Enhanced store UI since 1.9.0) | New in 1.4.0, on by default. Two columns in the Store's Buy list, between Item and U G N E (the `AddStoreColumn` slot): **Slot** (90 px, wide enough for "Glittering" and "Old Object"), what the item is worn as - its wear class, the game's own (`Character.WearClassOf`: the item type's gv1592 entry pointing into gv15C6, MDATA1) - as a short label: Weapon, Armor, Shield, Head, Hands, Body (the class MDATA1 calls Cloth: cloaks and robes), Feet, Wrists, Garment (sashes), Ornament (rings), Neck, Waist, Old Object, Element, Crest; the consumable classes (`MaxWornOfClass` 0) as Liquid, Particles, Parchment, Book, Stick, Round, Square, Glittering; Miscellaneous Object as Misc. The labels are keyed by the class's name in MDATA1, so a class a changed MDATA1 adds is shown whole. Its tooltip: the class's full name and the item type's, "Waist Protection - Belt". **Worn** (150 px), for a class worn one at a time (`MaxWornOfClass` above nought; blank for the consumables and Misc): what the character in command has equipped in that class now, named as the pack names it (`Loot.NameFor` at that slot's own knowledge, alignment and charges), or blank; what counts as worn is the game's own count (`Character.WornOfClass`: every equipped slot of the class). Cells take the row's bold or grey. The rows are drawn again on a character switch (the Store's UPDATE), a buy or sale, an equip or unequip in town (the rows only - not the Store's UPDATE, which the original does not send for an equip) and a mod setting changing. While the columns are shown the Store's window is wider by their widths plus 80 px more for the Item column (`ModSlots.StoreItemRoom`), so long names have more room than with none; the width the party's layout keeps for the Store is written back without that extra, so the saved layout is the same with or without the mod, and switching the toggle with the Store open widens or narrows it at once. In Objects & Spells (town and dungeon) the same label follows an item known at least Partially, dim grey, e.g. " [Waist]", after the identification mark if there is one (`PackRowDecoration.Then`); at Partial the line already names the item's type, from which the class follows, so it tells nothing more. Barely known: no label (the line then names the class itself). The Hands in pack slot 1 (MDATA3 item 0) have no label: they are the character's own, not worn |
| Store: sortable columns (part of Enhanced store UI since 1.9.0) | New in 1.5.0, on by default. The Store's Buy list sorts by a click on its headers: **Item**, by the item's name (trimmed, case ignored), A to Z; a second click Z to A; a third back to the shelf's own order, the original's (sub260A, FRMCTYS.BAS:512-555, lists by item index and nothing sorts). **Slot**, while the slot columns are shown: by the Slot column's label, then the name; a second click reverses both; a third, the shelf's order. One sort at a time (clicking the other header starts it at A to Z); ties keep the shelf's order. The sorted header carries a ▲ (first to last) or ▼ (last to first); a sortable header's caption shows the hand cursor, and a click anywhere on the header sorts. The sort is kept in settings.ini as the text setting `StoreSortOrder` (`Item`, `-Item`, `Slot`, `-Slot`, or empty), so the Store opens sorted as it was left, and a buy, sale, switch or equip redraws in the same sort. A Slot sort with the slot columns switched off draws the shelf's order (and Slot is not clickable) but is kept for when they come back. It sorts the rows listed: what "Store: hide items you can't use" hides is left out first. After a change of sort the highlighted row, if listed, is brought to the top of the list, otherwise the list starts at its top; a redraw with the same sort (a buy or sale) keeps the list where it was, as the original's does. While Select Alignment holds the list (the original's lock, FRMCTYS.BAS:981) a header click does nothing; with "Unlocked Store alignment choice" on it sorts. The search (txtSearch_KeyPress, FRMCTYS.BAS:895-935, walks the listed rows in the list's order) walks the rows in the drawn order, so it scrolls to the drawn place; what it finds is the same in any order but for two listed under one name, where the first drawn wins. Toggle off, or mods off: plain headers that do nothing, the shelf's order - the Store exactly the original's. Through `StoreRowOrder`, `StoreHeaderSort` and `StoreHeaderClicked` |
| Bank sort (always there since 1.8.0) | New in 1.6.0, on by default: a Sort button in the Bank, right-aligned on the "Deposit/Withdraw Items" caption's line, from the end of the caption's box to the list box's right side (the `AddBankCommand` slot). It puts the vault in the order the Objects & Spells Sort uses (`PackSort.KeyFor` and `PackSort.KeyOrder`, shared): worn classes first, then consumables, then Miscellaneous Object; within each by class, then the type where it is known (Partial or better; not-yet-known types first), then the name as shown, then the old order. The items fill the vault from its first slot and the empty slots (holes a withdrawal left) go last, so the list shows them sorted; the vault keeps its number of slots. Each record moves whole (charges, alignment, identification level, cursed bit); nothing else points at a vault slot, so nothing else follows. "Bank sorted." in Messages; the character is saved and the Bank (and an open Store) redrawn when anything moved. No paralysis test: the Bank's own moves have none. Toggle off, or mods off: no button, the Bank as it is without the mod |

All seventeen conveniences are built (eleven toggles since 1.8.0, six since 1.9.0, the combat log turned into colour-coded Messages lines, four and a choice since 1.10.0); planned mods 1 to 8 are all in this mod, 1.2.0 adds four more (auto-deposit, the Store's hiding, the identification mark, the shelving line) and 1.3.0 one (the party covering a shortfall). 1.3.1 changes the XP bar to two stages (green to the next level, then purple to the pin), shows the amount to the pin in the Next column once the level is earned, and adds the Store's own "Hide unusable" box. 1.4.0 adds the Store's Slot and Worn columns and the pack's slot label. 1.5.0 adds the Store's sortable Item and Slot headers. 1.6.0 adds the identification mark in the Bank's list and the Bank's Sort button. 1.7.0 adds the quest marks in the Next column (Mark active quests) and widens Next from 44 to 50 pixels for them. 1.7.1 puts the XP tooltip on two lines and keeps two pixels inside the bar at either end of the Next figure (with the host's columns now growing with a larger Options font, "!Pinned" in bold no longer fills the bar).

### The Kaizen mod (`src/Mods/Mordor.Mod.Kaizen`, id `Kaizen`)

Version 1.2.0 (1.0.0 agreed with the user 2026-10-04; 1.1.0 the same day: one buff list per character, edited in
the Kaizen window, in place of the Mods page's one table; 1.2.0, 2026-10-08, the user's request: the window's
drop-downs replaced by drag and drop, the way the spell buffers are set). It does the purely mechanical casting a player would otherwise do
by hand, at exactly two moments and never otherwise: no keys, no reacting to anything (a buff stripped by an
extinguisher square is not noticed), no questions. The principles it is held to: nothing depends on timing, it
reveals nothing the player was not told, it replaces no judgement (a simple mechanical rule, or the player's own
list), and every cast goes through the dungeon's own cast or Use, so spell points, charges, lines, sounds and
refusals are those of the same cast by hand. Neither part does anything while monsters stand on the square
(engaged or not): no cast can become a combat order. Off until ticked on the Mods page, and then both toggles are
off until ticked; each is read as its moment comes.

| Toggle | What it does |
|---|---|
| Buff on entering the dungeon | When the party goes down from town (the city's Dungeon picture or d; not a character loaded below ground, not after a raise below), every member's own **buff list** is run once: the members in party order, each list in its order, the list's owner the caster. Each row: **Spell or item** (a spell of the owner's, or an item in the owner's pack, by name) and **Target** (a party member, for a spell that asks "Cast on Who?"; none for a party-wide spell, which asks nobody by hand either). A spell row is cast through the sheet's Cast; a spell the owner does not know (any more) is still sent, and the game's own refusal says why. An item row finds the first pack slot whose item is known at All by that name, else the first the pack names so at a lower level (the item's name once Mostly known, the type's at Partial, the class's at Barely); an item is used only when known at All: below that the game shows nothing of what it casts, so nothing about it is looked at and the row is skipped with one line naming it as the pack does, `Kaizen: "Holy Sword" is not fully identified; use it by hand.` A row whose spell (or item's spell) asks "Cast on Who?" and whose target is not in the party now is passed over **without a word**: the lists are kept per character and parties vary. A party-wide row's target is not looked at (the cast by hand asks nobody), so it is done whoever it names. A dead member's list is skipped with one line, `Kaizen: "Gon" is dead.` Other skips, with the game's own refusal (not enough spell points, spell level or a stat too low, does not know it, "Anti-Magic area!", out of charges, an aligned item, ...) or, where the game has no words, a line of the mod's: `Kaizen: "Sight Veil" needs a target.`, `Kaizen: There is no spell called "X".`, `Kaizen: "Cas" has no item called "X".`, `Kaizen: "X" holds no spell.`, and `Kaizen: "Teleport" asks for more than a target; cast it by hand.` for a spell whose case asks something else (a map spell, a raise, a bind, a lock: only the effects `SpellThrown` handles are sent). Then the next row. A cast refused with "Anti-Magic area!" (an anti-magic or extinguish square) ends the whole run there, every later row and list, as a player would stop. Kaizen never chooses the spell, single or mass, or the caster |
| Heal before returning to town | On the stairs up out of level 1 (after a won round is finished, before the dungeon closes and before the town's rest; not a rescue, not Exit to Main Menu), a greedy loop: **target** the living member (companions are not looked at) missing the most hits, MaxHits − CurHits, party order on a tie, and stop when nobody is missing any; **caster** the first member in party order with a healing spell (`Spellbook.IsHealing`, of the heal effects) they can cast (`CastGate.Unfit` null) and afford (spell points at least `Spellbook.Cost`); **spell** that caster's affordable heal with the most hits per spell point on average, the average being `Rules.SpellHealAverage` at the caster's spell level (`Rules.SpellLevel` of the current guild's level, the level the cast heals at) with no cap at the hits missing, and for a party-wide heal (Mass Heal) that average times the living members missing hits; the first on a tie, in the spell page's order. Cast through the game, repeat. A cast refused, or one that spends no points, takes that caster and spell out for the rest of the loop; at most 250 casts; a cast refused with "Anti-Magic area!" ends the loop at once. Spells only, no items. The per-cast lines, refusals and sounds are the game's, reading as if the caster were in command (as switching to them and casting by hand would): `"Hal" threw "Heal" on "Bob".` |

The buff lists are made in the **Kaizen window** (the toolbar's **Kaizen** button after the icons, in town and in
the dungeon; one of the mod windows `AddWindow` gives: a child window of the game's frame like Objects & Spells, which takes no
keyboard and remembers whether it was open and where). A mod window is a child inside the frame, not a window the frame owns, so the dungeon's held-key replay, which waits
for the frame's open dialogs, never waits for it. A spell or item is dragged from Objects & Spells into it within the one frame. It shows the list of the character in command, and
another character taking command (a click in Party Information, ALT-1..4, n) shows theirs. From the top: a bold
heading `Buff list of "Cas"` in the Mods page's heading blue; one grey line with the gestures, exactly `Drag a spell
from the Spells list or an item from their pack to add a row. Drag a row up or down to move it. Double-click a row to
change its target. Drag a row out of the window or right-click to remove it.` (`KaizenView.Gestures`); in orange, only while "Buff on entering the dungeon" is off, that the lists are kept and cast once it is ticked on
the Mods page (reached in a game from the Control Panel's **Mods...**; the note follows the setting at once, through
`ModSettings.Changed`). Then the rows, one line each, numbered:
`1. Fire Shield → Oto`, `2. Mass Protection (party)` (a spell or item whose spell asks nobody "Cast on Who?",
`Buffs.AsksTarget`), `3. Rod of Light (item) → Cas`; "No rows yet." while there are none. What no longer holds is
marked in orange: a spell "(not known now)", an item "(item, not in the pack now)" (not in the pack known at All),
a target "Zed (not in the party)", a row that asks and has none "→ (no target)". The rows are edited by **drag and
drop, as the spell buffers are set** (1.2.0; the 1.1.0 drop-downs, ▲ ▼ ✕ and Add row are gone):

- **Add**: drag a line of that character's **Spells list** or **pack** (Objects & Spells, in town or in the
  dungeon - the very drag that sets a buffer, `ModDragged`) onto the window; the row goes in at the gap under the
  pointer (a line marks it while dragging), or at the end when let go below the rows or anywhere off their visible area (the heading included, however the list
  is scrolled). Only what the
  buffers would take from the character's own lists and the player has been shown (`Buffs.Judge`): a spell they
  know (`Spellbook.Known`) that Kaizen can send (`SpellThrown.Handles`; a map spell, a raise, a bind is refused with
  the run's line `"Sleep" asks for more than a target; cast it by hand.`), or an item in their pack known at
  **All** whose spell Kaizen can send (stored by its name). An item below All is refused with the run's line
  naming it as the pack does, `"Sword" is not fully identified; use it by hand.`, before anything about it is looked
  at (whether it holds a spell, or one that asks); at All `"X" holds no spell.` or `"X" asks for more than a target;
  use it by hand.` The Hands and an empty slot are refused silently; a line of another character's lists is
  refused (`Only "Cas"'s own spells and items go in this list.`). A refusal shows as one orange line under the
  gestures, until the next drop. If the spell (or the item's spell) asks "Cast on Who?" (`SpellThrown.WhoToAsk` not
  None), the game's own box asks it at once (`ModGame.AskCastOnWho`: the dungeon's list, titled `Cast "Fire
  Shield"`, the party in order, companions never) and the member picked is the row's target; Cancel cancels the
  drop. A party-wide one asks nothing. One box at a time: while it is up the window takes no drop, double-click or
  Change target, and a drop whose list changed meanwhile goes in at the end (`BuffEdits.GapAfter`).
- **Move**: drag a row (left button, past the system's drag rectangle, as the game's own drags) up or down within the window; it goes in at the
  gap marked.
- **Remove**: drag a row out of the window and let go there (while it is outside, the row dims and the orange line
  says `Let go here to remove row 2.`), or right-click it and choose **Remove**.
- **Target**: double-click a row (or right-click, **Change target**) to answer "Cast on Who?" again; Cancel keeps
  the target. A party-wide row says it asks nobody, and an item row whose item is not in the pack at All now says
  its target cannot be asked.

Every change is written at once. Nothing in the window takes the keyboard (the game's keys stay with the game), and
the "Cast on Who?" box is shown over the game's frame, so the keyboard goes back to the game when it closes. With
no party in play it says so and takes nothing. The window's text is the face and size of the game's list rows
(Objects & Spells' items), as every mod window's.

The lists are kept by the game in `kaizen.json` in the saves folder (beside settings.ini, `ctx.DataDirectory`;
`MORDOR_SAVES` moves it), keyed by the character's name (matched without regard to case or surrounding spaces):
`{ "Version": 1, "Characters": { "Cas": [ { "Kind": "Spell", "Name": "Protection", "Target": "Oto" }, { "Kind":
"Item", "Name": "Rod of Light", "Target": "" } ] } }`. Read when the mod loads, written whole (beside, then moved
over) each time a list changes; an emptied list takes the character out, and a character deleted on the main menu
takes their list with them (`ModEvents.CharacterDeleted`). A file that cannot be read is moved aside at once, before
anything can write over it, to `kaizen.json.bad-yyyyMMdd-HHmmss` beside it, the lists start empty, and the mod's log
(and crash.log) says so; one that cannot even be moved is never written over that session. The 1.0.0 `BuffList=`
key under `[Mod:Kaizen]` is no longer read or shown. Defaults: both toggles off, no lists. The mods probe
(`tools/check.sh`) asserts the heal choice (target, caster, spell, Mass Heal counted for the hurt), the loop's end
(refusals, free casts, the cap, monsters), the lists' storage (the file, its round trip, a garbled file moved aside,
a deleted character's list dropped), the run (party order, each list's order, the silent skip of an absent target
only for a spell that asks, a party-wide row's target ignored, the dead member's one line, anti-magic ending every
list), what a drop on the window takes and refuses (known spells, items at All only and nothing told of one below,
spells that ask more than a target, the target asked only when the spell asks, Cancel), the window's edits (moved,
put in, removed, retargeted) and that they are what is kept, `ctx.Game`, `AskCastOnWho`, `ModDragged`,
`ModSettings.Changed`, and `Rules.SpellHealAverage` against 200,000 rolls of `Rules.SpellHeal`, against a stand-in
for the dungeon's cast path (which the probe cannot open).

### The DPS Meter mod (`src/Mods/Mordor.Mod.DpsMeter`, id `DpsMeter`)

Version 1.1.0 (1.0.0 2026-10-21, the user's request; 1.1.0 2026-10-25 the XP mode, API version 8 - a save tagged with 1.0.0 is not told a mod is missing, the tag being matched by id). Off until ticked on the Mods page; no toggles. It only listens
(`ModEvents.DamageDealt`, and `ModEvents.ExperienceGained` for its XP mode) and changes nothing in the game. It is **not** held to the Quality of Life mod's "reveal
nothing the player was not shown" rule - the user's decision: it shows the true figures, the hits actually taken off
the monsters, shown in a caption or not (another member's blows, a killing blow's damage, a companion's silent blows).

The **DPS Meter window** (the toolbar's **DPS Meter** button after the icons, in town and in the dungeon;
an `AddWindow` window, so it takes no keyboard and remembers whether it was open and where), drawn as
the game's own windows are: a child window of the frame, the game's window background, no font of its own (the one the game gives every mod
window, Party Information's row font through the Options fonts, its heading line bold as that window's header), the Party window's text colours, no shadows or effects, and
each row the font's line and three pixels, as the port's list rows (`UiFonts.RowPad`). At the top, two
tab-like buttons, **Damage** | **XP**, for what the bars measure (the one shown pressed and bold; kept in the mod's
settings, `Mode`, as `Damage` or `XP`). In Damage mode a heading line ("Damage",
"Kills", bold, in the Party window's caption blue), then one row per member of the party in play, in party order: the
name, a horizontal bar, and the **Kills** column on the right. Each bar is split, left to right, into Physical |
Magical | Companion; the top damage dealer's bar is the full width and every other in proportion to it (nothing
dealt, no bar). The colours are the port's bar colours, kept once in `Mordor.Core.BarColours` and shared with the
Quality of Life mod's Party Information bars: Physical the Next column's green (50, 175, 95), Magical the spell
points bar's full blue (70, 150, 255), Companion the Next column's purple (205, 70, 185). The total is written over
the bar in plain text, as the Next column writes over its experience bar: the bar at half strength over its dark
track, the figure in the Party window's white, with thousands separators (1,234). A click on a row marks it as the
Party window marks its selected member (the name green and bold) and shows that character's shares at the
bottom, beside Reset (before any click, `Click a bar for more information.`): `Physical 1,520 (78%)   Magical 340 (17%)   Companion 75 (4%)`
(the shares of their own total, whole per cent, half away from zero; no name, total or kills, which the row already
shows); the same line is the bar's tooltip. In **XP mode** the heading reads "Experience" and each bar is the
experience the character's total went up by (`ModEvents.ExperienceGained`), split Damage | Chests - Damage every award
for damage dealt (their own blows, spells and charms, and their companions' damage credited to them at a quarter),
Chests a chest's trap disarmed - in the experience bar's green (50, 175, 95) and the hits bar's half-way yellow
(230, 210, 50); the XP total over the bar, no Kills column (its cells empty, so the bars line up); a click shows
`Damage 4,100 (94%)   Chests 250 (6%)`. Only what was actually added counts: an award the pin clamps counts what
reached the total. It follows every blow and award at once, and the party (a member joining or leaving).
**Reset** empties every character's tally at once, damage and experience, those not in the party included - no
question asked. With no game in play it says so.

What is counted, per character (`DpsTally.Of`):

- **Physical**: the hits their weapon's blows took off monsters - every swing of every hand (MODULE4.BAS:333), hits,
  criticals, backstabs, severs, and killing blows (the monster's last hits; what the roll had beyond them is not
  counted).
- **Magical**: the hits their damage spells took off monsters, single and group, cast by them or from an item they
  used (each monster the spell reached). A charm takes no hits off: a creature charmed walks off alive, so it adds
  its kill (the game counts it as the caster's kill) and no damage, though the game pays experience on its hits.
- **Companion**: the hits their companions took off monsters - breath, acid spit, blows, stone (the monster's hits),
  electrocution and spells - credited to the companion's owner. A companion's mishap on the party is not counted.
- **Total**: the three together. **Kills**: every monster the character killed (as their Kills on the character's
  page counts them: blows, spells, a charmed creature) **and** every one their companions killed - one Kills column,
  companions' kills included.

The tallies are kept by the game in `dpsmeter.json` in the saves folder (beside settings.ini, `ctx.DataDirectory`),
keyed by the character's name (matched without regard to case or surrounding spaces): `{ "Version": 1, "Characters":
{ "Cas": { "Physical": 1520, "Magical": 340, "Companion": 75, "Kills": 12, "XpDamage": 4100, "XpChests": 250 } } }` (a file
written before the experience was kept has no `XpDamage` or `XpChests`, and they read as nought). Written whole (beside, then moved
over) once a second has passed with no new blow or kill (a burst - a companion's breath across a group, a round of
swings - is one write, on a timer thread of its own, so the combat round never waits on the disk); Reset and a
deleted character are written at once, and whatever is still waiting when the program exits is written then
(`DpsTally.Dispose`, from `ProcessExit`: the mod system has no unload), so a crash loses at most the last second;
kept across sessions until Reset; a character not in the
party keeps their tally (it is shown again when they rejoin); a character deleted on the main menu takes theirs
with them. A file that cannot be read is moved aside to `dpsmeter.json.bad-yyyyMMdd-HHmmss` and the tallies start
empty, as kaizen.json. The `mods` probe asserts the event against the fights themselves (every swing of a party of
two over 300 fights, the watched one and not: each "You hit for N damage!" told as N, a miss as nothing, a kill as
its last hits and the kill, the amounts adding up to the hits the monsters lost and the kills to each one's Kills;
single and group spells, a character's own and an item's, each "damaged one for N" told as N; companions' breath,
spit, blows and spells all the owner's, adding up with the owner's blows to the hits lost and every monster that
fell; the same fights played with and without a listener), a charm raising its kill and no damage, the experience (every blow's, spell's and companion's damage followed at once
by its award to the same character, the awards adding up to each member's experience gained, a chest's disarm of the
Chest source, an award the pin clamps counting only what was added, one already pinned counting nought), the tallies (each kind's column, the experience by source and its round trip, a file from before it, a burst of events written once and only once it is quiet, what is waiting written on closing, the round trip, Reset,
a deleted character, a garbled file) and the mod loaded through the host; `Mordor.UiCheck` drives the window
headless (bars in party order, segment widths in proportion, the overlay, a click's shares, live rescaling, the Damage | XP
buttons and the mode kept, the XP bars and shares, Reset at once).

### The Cartographer mod (`src/Mods/Mordor.Mod.Cartographer`, id `Cartographer`)

Version 1.0.0 (2026-10-26, the user's request): "Mark squares on the automap with colours and waypoints". Off until
ticked on the Mods page, as the others; no toggles. It is purely the player's own annotation of the Automap: it reads
nothing of the maze (any square of a level the map can show may be marked, explored or not), reveals nothing, and changes
nothing in the game. With the mod off - or with no mod - the Automap is exactly the original's: the right button
re-plots at once and nothing is drawn over the map (the `AutomapHooked` test, below). It has no window and no Avalonia
of its own: the game draws its menu and marks through the Automap hooks (API version 13).

- **The menu.** A right-click on a square of the map (picMaze; not the grey round it, the numbers or the level row)
  opens a small menu in the game's menu style (the Kaizen window's, in the Automap form's own font) instead of
  re-plotting. Its **first item is the original's own right-click**, `picMaze_MouseDown`'s full re-plot, under the help's
  name for it, **Redraw/Refresh Level** (the game puts it there whatever the mods offer, and it does exactly what the
  right button does with the mod off); then a line; then **Blue**, **Orange**, **Red**, **Purple**, each with a swatch of
  its colour: Tableau 10's first three (#4E79A7, #F28E2B, #E15759) and a neon purple, #BC13FE (in place of Tableau's
  teal, which read too close to the blue; a cartographer.json written before keeps its squares, its `"Teal"` read as
  Purple and written back so); **No colour** (greyed when the square has
  none); a line; and **Add waypoint...** - or, for a square that has one, **Change waypoint...** and **Remove waypoint**.
- **Colours** tint the square's inside, inside its edge lines, at 60% (`AutomapMark.Tint`): the map shows through, and
  the walls and doors on the square's edges are never covered. One colour per square; a new one replaces it.
- **Waypoints** link a source square to one destination square, on any level (one per source; the destination is not
  marked). **Add waypoint...** asks for the destination (`AutomapView.PickSquare`): the Automap's status line (lblInfo,
  "You are at ...") reads `Click the destination square (any level). Esc cancels.` and the pointer is a cross; the level
  bar, its arrows and < > page the map as ever; a left click on a square takes it (the square under the pointer is
  framed white meanwhile), and **Escape** (from any window of the game, while the pick waits) or **a right-click**
  gives up, leaving everything as it was. A click on the source itself changes nothing. A source square is drawn in
  Tableau's yellow, #EDC948, apart from the four colours: tinted yellow when it has no colour of its own, and always
  framed yellow (`AutomapMark.Frame`), so a coloured source keeps its colour inside the yellow frame.
- **The hover**: with the pointer over a waypoint's source, the source is framed white (bold), and when the destination
  is on the level shown an arrow in the waypoint yellow, outlined dark, runs from the source's middle to the
  destination's (`AutomapMark.Arrow`); for a destination on another level, the frame alone.
- **A click** (left) on a waypoint's source pages the map to the destination's level (the level bar's own way,
  `AutomapView.ShowLevel`) and frames the destination white (bold); the highlight stays until the next click on a square
  of the map. A double click still resizes the window, as the original's does. "Auto Change Lev." still follows the
  party to its own level at its next step, as without the mod.

The notes are kept per world - the shared automap (automap.json) is the saves folder's, and so are they - in
`cartographer.json` in the saves folder (`ctx.DataDirectory`, beside settings.ini; `MORDOR_SAVES` moves it):
`{ "Version": 1, "Colours": [ { "Level": 1, "X": 5, "Y": 7, "Colour": "Blue" } ], "Waypoints": [ { "Level": 1, "X": 5,
"Y": 7, "ToLevel": 3, "ToX": 12, "ToY": 2 } ] }`, the squares counted from one as the Automap's readout counts them
("You are at 5,7,1"). Read when the mod loads; written whole (beside, then moved over) at each change. A file that cannot
be read is moved aside to `cartographer.json.bad-yyyyMMdd-HHmmss` and the notes start empty, as kaizen.json; an entry that
makes no sense (an unknown colour, a level below one, a square below one, a waypoint to its own square) is left out.
The `mods` probe asserts the manifest and default, nothing hooked with mods off, the menu's items and swatches, colouring
and clearing, waypoints picked across levels, given up and replaced, the marks (tints, the source's yellow, the hover's
frame and its arrow on the same level only), a click going to the destination and highlighting it until the next click,
the file's round trip, a hand-edited and a garbled file, switching off and on, `AutomapLayout.SquareAt`, and the hooks
guarded (a mod throwing in its marks, or in a pick's answer, disabled). `Mordor.UiCheck` drives the real Automap child
headless: mods off (the re-plot at once, no menu, nothing drawn), the menu (its first item the re-plot, reached through
it), colouring, a waypoint picked on another level through the level bar, Escape and the right button giving up, the
hover's arrow (none across levels), the click's navigation and highlight, a new session reading the file back, the mod
switched off taking its marks away at once, and the marks rendered on the map's own device-pixel squares at 1x,
enlarged, and at 125% (the tint filling exactly the square's inside, the edge lines untouched, the frame inside them).

### The Auto-combat mod (`src/Mods/Mordor.Mod.AutoCombat`, id `AutoCombat`)

Version 1.0.0 (2026-10-30, the user's request: "an auto-combat mod, similar to Kaizen ... basic rules: if an enemy
is in melee range and no party member is injured and at least X action points are available, do Y; target the
enemy with lowest HP; target healers first, then casters, then melee"). Off until ticked on the Mods page, and then
both toggles are off until ticked, each read at every round: **Fight by the rules** (`FightByRules`) and **Leave the
one in command to me** (`LeaveInCommand`: the character in command keeps the orders given by hand). It gives each
member's fight order every round of an engaged fight, through `ModActions.CombatRound` (API version 14), and does
nothing else: no fight is started by it (it is asked only at the tick of an engaged fight, never while monsters
only wait, never for the free round a step gives them), nothing is cast or used there and then (an order is what
the round then plays, as by hand), and every order goes through the game's own keys and Cast, so its checks,
refusals and lines are the game's.

**What the game has, and what the user's examples become.** The original's combat is a one-second real-time
round in which each member carries out a standing order - None, Fight, Defend, or a spell recast each round
(`.Att`, `.M56C4`, FIDELITY.md "Standing orders") - aimed at one of up to four monster groups (`M547A`, the
number keys). So a rule's action is one of those orders and its target a group:

- "An enemy in melee range": there are no ranges - every monster in an engaged fight is on the party's square.
  The rules are asked only in an engaged fight, which is that condition; **groups at least / at most N** stands
  in for "how many".
- "No party member injured": **everyone's hits at least N%** (100% is nobody hurt), from the Party window's own
  figures; and **someone's hits below N%**, **own hits below N%**.
- "At least X action points": the game has no action points. The one resource a fight spends is **spell
  points**: **spell points at least N**. A Cast rule is also passed over when the member has fewer points than
  the spell costs (the spell page's figure) - so "cast if I can afford it, else fight" is two rules.
- "Do Y": **Nothing** (space), **Fight** (f), **Defend** (d), **Cast** a spell the member knows, **Use** an item
  known at All that holds a spell. A heal or a cure cannot be the order: the game refuses every non-combat spell
  in an engaged fight (mdrcharCheckCast, MAINMOD2.BAS:512-519), so the window does not offer one and a rule naming
  one is passed over.
- "Target the enemy with lowest HP": the game never shows a monster's hits ("You cannot see a monster's hit
  points", MODULE4.BAS:4392-4398), so the mod cannot read them. What the list does show is each group's count
  (for a monster of groups of two to twelve): **\*fewest** aims at the group with fewest standing (a group shown
  without a count counts as one), **\*most** at the most, **\*first** and **\*last** by list order.
- "Healers first, then casters, then melee": monsters carry no such role the player is shown. A target is a
  **name as the monster list shows it** - fn0E23's name for the one in command: the monster as the party
  believes it (a misidentified group answers to what it is believed to be), its **class** while unidentified
  (met in fog), "Unseen Monster" when invisible to them - tried in order, so "Priest, Mage, \*most" goes for a
  group shown as a Priest, else a Mage, else the biggest. **A group shown as** is the matching condition. A name
  matches whole words, case ignored, in the group's name for one (`MonsterGroupView.Singular`, so "Werewolf"
  finds the Werewolves) or as shown now: "Rat" is a Giant Rat, "Orc" is not a Sorcerer. The window offers the
  names the monster list has shown in a fight while the mod ran (kept, as shown for one, under the list setting
  `SeenNames`), the monster class names, "Unseen Monster", and \*first, \*last, \*most, \*fewest.

**The rules.** An ordered list; each round, for each living member who is not held fast (sub1019 does nothing for
the paralysed, MONSMOD.BAS:291), in party order, the first rule whose conditions all hold and whose order can be
given now wins: at most one decision per member per round. A member opening a chest (o: the Option column's
"Open") is left to it, since any order would call the count off, as the keys do. A rule is **For** anyone or one member by name; its
conditions are the ones above (each optional); **Do** and, for Cast and Use, the spell or item; and **Aim at**, the
target list (for Fight, Cast and Use). A member no rule fits keeps the order they have. The decision is then given
only where it differs from what stands: the aim first (a number key), then the order - so a rule that goes on
holding sends nothing more, and the Party window's Option column and the list's mark show what is standing. A
Cast is passed over when the member does not know the spell, cannot afford it, or the game refused it earlier in
this fight (the refusal is said once, by the game; another fight asks again - so an "Anti-Magic area!" refusal
stands for the rest of that fight even if the party steps off the square within the area); a Use when no pack
slot holds an item of that name known at All with a combat spell (below All nothing about the item is looked
at), or that slot's item was refused in this fight. A spell order is given as a spell buffer fires it (MAINMOD2.BAS:4151-4158: M5766 = -1, so
an item order standing is let go rather than drained), an item order as Use (MAINMOD2.BAS:6129-6137, with the
sub0BDE it runs at 6132); Fight, Defend, Nothing and a spell order call off a chest count first, as f, d, space
and a buffer key do (MAINMOD2.BAS:3594-3600, 4011-4016, 4040-4044, 4137-4145); a refusal is worded as if the
member were in command, as Kaizen's casts are.

The rules are kept in settings.ini as a list setting (`ModSettings.GetRows`, key `Rules` under `[Mod:AutoCombat]`;
not declared in mod.json, so the Mods page draws no editor for it): one row per rule, the cells `Who`,
`PartyBelow`, `PartyAtLeast`, `SelfBelow`, `SpAtLeast`, `GroupsAtLeast`, `GroupsAtMost`, `Shown`, `Do`, `What`,
`Targets` (comma-separated), a blank cell a condition not asked. A row that makes no sense (an unknown action, a
number that is not a whole number at or above nought) is left out, never guessed at. Defaults: both toggles off,
no rules.

The **Auto-combat window** (the toolbar's **Auto-combat** button, an `AddWindow` window as Kaizen's): the heading,
a grey line saying how the rules are used, in orange while Fight by the rules is off that the rules are kept and
used once it is ticked; the rules, one numbered line each (`1. Cas: if nobody hurt, 10+ spell points → Cast
"Firebolt" at Kobold, *most`), a click picking one; **Add**, **Copy**, **Up**, **Down**, **Remove**; and the picked
rule's editor. Everything is chosen with the mouse - drop-downs and buttons, none focusable - because a mod window
takes no keyboard (the frame sees every key first: a letter typed into a box would be a dungeon command, f is
Fight). The drop-downs offer only what the game shows: the party's names, the combat spells the rule's member knows
(anyone's for Anyone), their pack items at All holding one, percentages, spell points and group counts, the monster
class names and "Unseen". Every change is written at once.

The `mods` probe asserts the manifest and defaults, nothing made or asked with the mod off, with no host, or with
a running mod that has no round hook, and the rules through the host against a stand-in for the dungeon's
commands (which records them): first rule that fits per member in party order, aim before order, orders standing
not sent again, the user's three examples as above, a member short of points passed over, a refused cast not asked
again in that fight but in the next, a heal never sent, a spell not known passed over, the dead and the held fast
left alone, a member opening a chest left to it, Leave the one in command to me, the shown-name (whole words,
the name for one), group-count and hits conditions, \*fewest/\*most and a name's aim, the names kept for the
window, an item below All not looked at, one at All made the order once and a refused Use not asked again, the
same spell standing from an item sent again as a Cast, nonsense rows left out, the context closed once the hook
returns, and a mod throwing in its round hook disabled. The dungeon's own commands (`DungeonWindow.Mods.cs`) are
not reachable headless; their source is checked instead: the hook right before the round, the token rather than
the engine, the list's count rule, the chest count called off, d refused for the held fast, the "you" wording,
Use below All refused first and then sub0BDE, and every command's test that the fight and the member are live.

## How to write a mod

### Where mods go

The loader looks, in this order, in

1. `mods\` beside the game folder - beside `saves\`, where settings.ini lives. For the user's install that is
   `Desktop\mordor\mods\`. Put your own mods here.
2. `mods\` beside the program (`src\Mordor.App\bin\<config>\net8.0\mods\`), where the build puts the mods that come with
   the port (`src/Mods/ShipMods.targets`; a publish puts them in its own `mods\`). In an installed copy that is
   `%LOCALAPPDATA%\OpenDejenol.Game\current\mods\`, which each update replaces: put your own mods in the first.

`MORDOR_MODS` (folders separated by `;`) replaces both. A mod id found twice is taken from the first folder.

```
mods\
  MyMod\
    mod.json
    MyMod.dll          (and any private dependencies it needs)
```

### mod.json

```json
{
  "id": "MyMod",
  "name": "My Mod",
  "version": "1.0.0",
  "author": "you",
  "gameVersion": "1.1",
  "assembly": "MyMod.dll",
  "entryType": "MyMod.Entry",
  "description": "One line: the tooltip of the mod's heading on the Mods page.",
  "settings": [
    { "key": "Loud", "label": "Loud mode", "description": "The checkbox's tooltip.", "default": true }
  ]
}
```

A mod may also declare list settings, `"tables": [ { "key": "Aliases", "label": "Aliases", "description": "...",
"columns": [ "Name", "Shown as" ] } ]`: the Mods page draws an editor for each under the toggles
(free-text cells, rows added, moved and removed), also while the mod is off, and keeps it as one line under the key
(cells joined by `|`, rows by `;`, a backslash before either or a backslash inside a cell); the mod reads it with
`ctx.Settings.GetRows(key)` (a `string[]` per row) and may write it with `SetRows`. A list that belongs to a
character, or that needs choices from the game (what a character knows), is better as a mod window over
`ctx.Game` with a file of the mod's own in `ctx.DataDirectory`, as the Kaizen mod's buff lists are.

And choice settings (API version 10), `"choices": [ { "key": "Mode", "label": "Mode", "description": "...",
"options": [ "One way", "The other" ], "optionDescriptions": [ "...", "..." ], "default": "One way" } ]`: the Mods
page draws the caption, a radio button per option in a row (each button's tooltip its own line from
`optionDescriptions`, API version 12, else the description), above the toggles and also while the mod is off; settings.ini keeps the chosen
option's text under the key, and `ctx.Settings.GetChoice(key)` gives it back as declared (case ignored; an unknown or
missing value reads as the default, or the first option).

No description is written on the Mods page (since 2026-10-25): the mod's is the tooltip of its heading, a toggle's of
its checkbox, a choice's of its caption and buttons, a list setting's of its caption. A mod with many settings may
ask for a window of its own instead (API version 11), `"settingsWindow": { "title": "My Mod settings", "groups": [
{ "label": "Sound", "description": "...", "keys": [ "Loud", "Mode" ] } ] }`: the Mods page then shows a
**Settings...** button for the mod in place of its toggles and choices (its list settings stay on the page), on the
main menu's page and the in-game one, also while the mod is off - these are settings, not the mod switched on or off.
The window (`ModSettingsWindow`, in the Mods page's style) draws each group in a box: the heading, then the settings it
names in order, a toggle as a checkbox and a choice as radio buttons, each with its description as the tooltip. A
group holding toggles has a checkbox on its heading (the group's description its tooltip) that is ticked when all of
them are on, empty when all are off and mixed otherwise; a click on it switches them all on, or all off when they are
all on. Every change is written to settings.ini at once, and a change made elsewhere moves the boxes. Before the
window reads anything, the mod's own migration runs (`ModHost.MigrateSettings`: its entry type's
`public static void Migrate(ModSettings)`, if it has one, loaded but not switched on), also with the mod off, so the
window shows what the mod will use. One window per mod: a second click on Settings... brings the open one forward.

`id` names the settings.ini section (`[Mod:MyMod]`, holding `Enabled=` and each setting). `gameVersion` is compared
with `ModApi.GameVersion`; a mismatch is logged, and the mod loads anyway. Settings declared here show on the Mods
page even while the mod is off.

### The project

A .NET 8 class library referencing `Mordor.Modding` (the API) and, as needed, `Mordor.Core` (the game's types:
`Character`, `Item`, `Party`...). Mark those references `Private="false"`: the game's own copies are used at run time.
`src/Mods/Mordor.Mod.QoL/Mordor.Mod.QoL.csproj` is a working example.

```csharp
public sealed class Entry : Mordor.Modding.IMod
{
    public void Load(IModContext ctx)
    {
        var s = ctx.Settings;
        ctx.Events.MessageLogged += line => { /* observe */ };
        ctx.Decisions.StoreAllowsRowChangeWhileChoosing = () => s.Get("Loud");
        ctx.UI.ToolbarTooltip = b => b.Key == null ? b.HelpName : $"{b.HelpName} ({b.Key})";
        ctx.UI.AddObjectsCommand(new ObjectsCommand("Tidy", c => { c.Say("Tidied."); return true; }, () => s.Get("Loud")));
        ctx.UI.AddPartyColumn(new PartyColumn("HP%", m => $"{100 * m.CurHits / Math.Max(1, m.MaxHits)}%"));
        ctx.Log.Info("ready");
    }
}
```

`IModContext` gives:

- **Events** (observe only): `MessageLogged(string)`, `CharacterSwitched(CharacterSwitch)`, `StoreRowClicked(StoreRowClick)`,
  `CaptionShown(Caption)` - a line the dungeon shows only in a caption: `Kind` (CharacterHit, MonsterHit, JoinOffer,
  GoldShare, Chest, Trap), `Text`, and the `Character` it is about where there is one. Raised where the caption is
  set (DungeonWindow), never for an emptied one; the join offer and the trap readout only when they change, since they
  are repainted with the group or the chest (a chest from another encounter is told again even when its readout
  reads the same). With no mod loaded the call returns at once and the caption is the original's.
  `ArrivedInTown(TownArrival)`: the party is back in town from the dungeon - `Party`, `Selected`, and `How`
  (`Walked`: the stairs up out of level 1, alive; `Rescued`: carried out dead, the dead screen follows). Raised after
  the town's rest and after whatever `TownArrival` asked for; not at the game's start, not for Exit to Main Menu
  (which goes to the title), not for a session ending below. `StoreShelved(StoreShelf)`: a sale in the Store put
  the item on the shelf (`Character`, `Item`, the row's `Alignment`, and `Say` to write a Messages line); raised
  where the Store adds it, and only then. `CharacterDeleted(name)`: Delete Character on the main menu deleted a
  character (raised once its save is gone; the only way one is deleted), for a mod that keeps something per
  character - the Kaizen mod drops its buff list.
  `DamageDealt(CombatDamage)` (API version 5): hits taken off a monster, or a monster killed, by a party member or
  one of their companions, raised by the combat engine (`CombatEngine.DamageDealt`, which the dungeon sets as a fight
  begins) as it happens - the true figures, shown or not. `Character` (the one who dealt it; for a companion, its
  owner), `Kind` (`Physical`: a blow of their weapon, every swing of every hand, criticals, backstabs and severs
  alike; `Magical`: a damage spell of theirs, single or group, their own or an item's - the item's cast is the same
  `CastSpell` at the item's level; `Companion`: a companion's breath, spit, blow, stone, electrocution or spell),
  `Amount` and `Kills`, and `Shown` (the character is the one in command, whose captions are on screen). Two kinds
  of event: damage - one per monster struck, `Amount` the hits it actually lost, what was rolled capped at the hits
  it had left (`CombatEngine.Credit`, the figure experience is paid on: an overkill past nought is not counted),
  raised only when above nought, `Kills` 0 - and a kill, `Amount` 0 and `Kills` 1, raised as the monster dies: for a
  character where the game adds it to their Kills (`KilledBy`: a killing blow, a spell's kill, a creature charmed,
  which the game counts as the caster's kill), for a companion where its kill runs sub0CF3 or its spell kills (the
  game puts those on nobody's Kills). A killing blow so raises its damage, then the kill. A record the stone left
  behind that is no longer one of the fight's monsters (Combat.cs, the electrocution after a stone) is nobody's
  damage, and so is a creature charmed: it leaves alive with its hits, so a charm raises its kill and no damage
  (the game still pays the caster experience on those hits). Raised from inside the engine, so a handler must only observe; with no mod listening the engine does
  nothing more (no dice drawn, nothing changed - the `mods` probe plays the same fights with and without a listener).
  `ExperienceGained(CombatExperience)` (API version 8): experience put on a party member's total, raised by the
  combat engine (`CombatEngine.ExperienceGained`, set by the dungeon as a fight begins and on a chest) once for every
  award sub0F11 (MAINMOD2.BAS:279-310, `CombatEngine.GiveExperience`) makes. Its only callers are the two sources:
  `Damage` - sub0CDF (MONSMOD.BAS:468, 495; `CombatEngine.Credit`), the character's own blow, spell or charm, or a
  companion's damage credited to its owner at a quarter (`Rules.ExpAwarded`) - and `Chest`, a chest's trap
  disarmed (MONSMOD.BAS:3508-3509, `Rules.ChestExp`). `Character` (whose total it went on), `Amount` and `Source`.
  `Amount` is what the total actually went up by: the award, or less when the cap that pins the character clamped
  it - nought for an award to a character already pinned, and never below nought (a total past a lowered pin is
  pulled back to it, which is not counted as a loss). Raised right after the award, after any DamageDealt for the
  same blow; observe only - with no mod listening nothing more happens, and listening changes nothing.
- **Decisions** (null = the original's answer): `StoreAllowsRowChangeWhileChoosing` (any mod saying true unlocks it),
  `CityAltP` (first non-null answer wins; return `new CityAltPAnswer(CityAltPAction.PoolAndDeposit, "line")`),
  `MessagesKept` (how many lines the Messages window keeps; the original's 20, mc04F2, unless the first non-null
  answer says otherwise; at least 1), `TownArrival` (asked as the party arrives in town, with the same
  `TownArrival`; first non-null wins; `new TownArrivalAnswer(TownArrivalAction.PartyDeposit, total => "line")` (the total a `VbCurrency`: gold is VB Currency, four places, and shows through `CharStatFile.FormatNumber`) has
  the town do the Bank's Party Deposit for everybody, then look at the Wealthiest record;
  `new TownArrivalAnswer(TownArrivalAction.PartyPool, total => "line", member)` (API version 12) its Party Pool,
  everyone's gold on hand into that member's account (none named, or one not in the party: nothing moves); either is
  all or nothing on an Overflow; the line is asked for only when something moved; the original does nothing), `StoreRowVisible` (a Store Buy row about to be
  drawn: `Character`, `Item`, and `Usable`, the Store's own bold/grey test; any mod answering false hides it; the
  search then looks only through the rows listed), `CoverShortfall` (a town charge the payer's purse, gold on hand
  and bank, falls short of: `Party`, `Payer`, `Cost`, `Shortfall` (a `VbCurrency`, as is each gift's `Amount`); first non-null wins; `new ShortfallAnswer((gift,
  payer) => "line", from)` has the members named in `from` (API version 12; null: every other member in party
  order), in that order, hand over exactly the shortfall from their bank accounts - never their gold on hand; the
  payer, anyone not in the party, below ground or dead passed over (`Party.CoverShortfall`) - onto the payer's hand,
  which charPay then takes with the payer's own bank; the line is asked once per giver after the gold has
  moved; if they together cannot cover it, or the payer's hand would overflow, nothing moves and the original's refusal follows. Also asked,
  without moving anything, by the town's purse tests ahead of a charge. Never asked in a party of one, below
  ground, or with nothing short), `StoreRowOrder` (the Store Buy list's order: a `StoreOrderQuery` - `Character`,
  `Rows`, the rows listed in the shelf's order with what `StoreRowVisible` hides already left out, and `Lookup` -
  asked each time the list is drawn; first non-null wins; the answer must be the same `Item` objects, each once,
  reordered, or the mod is disabled for the session; null is the original's shelf order. The search walks the rows
  in the order drawn), `TownIncomeBank` (API version 12, in place of 10's `TownIncomeToBank`: a `TownIncome` -
  `Party`, `Character`, `Amount`, `Kind`: `StoreSale` or `CompanionSale` - in, the member whose bank account it is
  paid into instead of the seller's hand; first non-null wins; an answer not in the party counts as none; the
  original's hand otherwise, with the same overflow refusal for wherever it goes). No decision touches the
  dungeon's gold: API version 10's `PartyGoldTo` is gone in 12, and monGetGold's even share is the original's.
- **UI**: `PackRowDecoration` (a pack line on the Items page of Objects & Spells, town and dungeon: `Character`,
  `Item`, `Slot`, `Index` in; a `PackRowDecoration(suffix, colour, tooltip)` drawn after the name, or null for
  nothing; first non-null wins; its `Then` (null by default) is a further piece drawn after it with its own colour and tooltip, so one mod can draw a mark and a label; the lines are drawn again when a mod setting changes),
  `ToolbarTooltip` (the button's help name and key in, the tip out; first non-null wins), `AddObjectsCommand`
  (a button the size of Equip/Use/Drop/Info, stacked upwards from Equip, since frmCharStat3 has no room below Info; return true when the pack changed and the game saves and redraws),
  `BankRowDecoration` (a line of the Bank's Deposit/Withdraw Items list: a `BankRowInfo` - `Character`, `Item`, `Slot`,
  `VaultSlot` - in; a `PackRowDecoration` drawn after the name, or null; first non-null wins; empty vault slots have no
  line and are not asked about; the list is drawn again when a mod setting changes), `AddBankCommand(new
  BankCommand(label, run, isShown))` (a button right-aligned on the Bank's "Deposit/Withdraw Items" caption line, in
  load order; `run` gets a `BankCommandContext` - the character in command, `Say`, the item table - and returns true
  when the vault changed, and the game then saves, redraws the Bank and tells an open Store; `isShown` is read again
  on every mod-setting change. With no live mod adding one, the Bank has only its own controls),
  `AddPartyColumn` (a column after Option; a cell is a string or an Avalonia Control; `PartyColumn.WithTables(header, q => ...)` makes one whose cell gets a `PartyCellQuery` - the member and the monster and item tables by id, for a cell that names a quest's target - where the constructor's gets the member alone; while it is shown the window's
  contents are drawn wider by its width and scaled to the window, so Name and the rest keep their room and the
  window's saved size is untouched; the width is in the default fonts' pixels, and in a larger Options font it is
  as much wider as the Party drawing is, `CityWindow.PartyGrowthX`, and never narrower than the heading; each cell
  is exactly the column's width, a string set right in it and a Control inside a Border of that width, so no
  row's content moves another row's cells; and while one is shown, a new character's Automap is placed clear of
  the Party window where there is room, `WindowLayout.Arrange`'s `partyWidened`), `AddStoreOption(new StoreOption(label, get, set, isShown, tooltip))` (a check
  box on the General Store's Buy Items heading line, right-aligned, in load order: it shows `get()`, a click calls
  `set(value)`, and it is read again - ticked, shown or hidden - whenever a mod setting changes; the Store's list
  is redrawn after a mod-setting change too, so a box bound to a setting that a Store decision reads takes effect at
  once. Bind it to one of the mod's own toggles and the Mods page and the Store flip the same setting. Each call is
  guarded like the other slots: a throw disables the mod and the box goes. With no live mod adding one, the heading
  is the original's alone), `AddStoreColumn(new StoreColumn(header, cell, isShown, width))` (a column in the
  General Store's Buy list, between Item and the U G N E counts, in load order; `cell` gets a `StoreCellQuery` -
  the character in command, the row's item, the item table - and returns a string, a `ModCell(text, tooltip,
  colour)` or null; drawn in the row's own bold or grey unless the cell gives a colour. Cells are read whenever
  the rows are drawn: a character switch, a buy or sale, an equip in town, a mod setting changing. While any
  column is shown the Store's window is wider by the shown columns' widths plus 80 px for the Item column, and the
  width saved for the Store is written without that extra; `isShown` is looked at again on every mod-setting
  change and the window resized with it. Guarded like the other slots. With no live mod adding one, the list is
  the original's), `StoreHeaderSort` and `StoreHeaderClicked` (sortable Store headers: `StoreHeaderSort` is asked
  with a header's text - "Item" for the Store's own column, a Store column's own header while it is shown - each
  time the list is drawn, and answers null (the original's plain header, clicks do nothing) or a `StoreSortArrow`
  (`None`, `Ascending` ▲, `Descending` ▼) drawn after the text; the first non-null answer's mod owns the header,
  and a left click on it calls that mod's `StoreHeaderClicked(header)`, after which the list is drawn again (by the
  mod-setting change the click made, or by the Store itself when it made none). The host wires clicks only for a
  header some mod answers for; while Select Alignment holds the list with the original's lock a click does
  nothing. A change of the marks counts as a re-sort: the highlighted row is brought to the top, or the list starts
  at its top; any other redraw keeps the list where it was. The order itself is `StoreRowOrder`), `AddWindow(title, build, isShown)`: a game-style window, one of each at most -
  a child window inside the game's frame (`MdiChild`), beside Objects & Spells and Party Information, with their
  caption bar, borders, sizing grip and minimise / maximise / close boxes, stacked among them (a click brings it
  forward), and taken down to the dungeon and back up with the party as the game's own children are. Its content
  is drawn in the face and size of the game's list rows (Party Information's names, Objects & Spells' items: MS Sans
  Serif 8.25 through the Options fonts), on the game's child-window background. It takes no keyboard: a click does
  not make it the active child (the town's Alt keys and a service's keys stay where they were), and the frame's key
  preview still sees every key first. The frame is the game's window at the top of the owner chain - the city -
  never the Control Panel or the Mods page, so opening it from the Mods page does not tie it to a dialog that
  closes first. There is no frame on the main menu, so it cannot be opened there. The toolbar carries a button with its title after the icons (hidden on the toolbar's info page and
  with No Toolbar, like the icons) that opens and closes it (the Mods page has no Open buttons). Whether it is
  open and where (`Window.<Title>.Open` and `.Place` in the mod's settings.ini section: left, top, width, height in
  the frame's units and the state - 0 normal, 1 minimised, 2 maximised - as a game child's placement is kept) is
  kept, so it opens again, in its place, in the next game. A saved place is kept whole inside
  the frame area in view and no larger than it (placed once the area has a size: the dungeon's is laid out after
  its toolbar first looks); a maximised window moved to the other frame area fills that one. A four-number place
  (the screen pixels the earlier top-level windows were kept at) is not used, and with none saved it opens in the
  middle of the frame. While `isShown` says false the button is gone and the
  window closed, and it comes back when it says true if it was open. A cell or window that names Avalonia types is
  best reached through a small method returning `object` (see `QolMod.Draw`): .NET resolves an Avalonia type when the
  method naming it is compiled, and the headless `mods` probe has no Avalonia.
- **The Automap** (API version 13, for the Cartographer mod): four hooks on `ctx.UI`, each handed an `AutomapView` - the
  map open now: `ShownLevel` (hsbLevel's value), `DeepestLevel` (its Max), `LevelSize(level)` (squares across and down),
  `ShowLevel(level)` (page the map as its level bar does, held to 1 and the deepest), `Refresh()` (ask every mod for its
  marks again and draw them), `PickSquare(prompt, done)`, `CancelPick()` and `Picking`. Squares are `AutomapSquare(Level,
  X, Y)`, X east and Y north from nought (the readout adds one). **With no running mod hooking any of the four
  (`ModHost.AutomapHooked` false), the Automap does nothing it did not do before**: the right button re-plots at once
  (`picMaze_MouseDown`), a hover or a click is not looked at, and the overlay is hidden.
  - `AutomapMenu` (`(view, square) => items`): asked at a right-click on a square of picMaze (where `OnPicMaze` finds
    one: not the grey round it, the numbers or the level row). When the running mods offer at least one item (in load
    order, `AutomapMenuItem.Separator` kept between them), the right-click opens a menu - a ContextMenu at the pointer,
    in the Automap form's font - whose **first item is the original's right-click**, **Redraw/Refresh Level**
    (`AutomapWindow.RedrawItem`, the full re-plot the right button does with no mod), then a line, then the mods'
    items; with none it re-plots at once, as the original. An `AutomapMenuItem(Label, Run, Swatch, IsEnabled)` shows a
    12-pixel square of `Swatch` before its label; `Run` is guarded, and the marks are asked for again after it.
  - `AutomapMarks` (`view => marks`): what to draw over the level shown, asked whenever the level is drawn (a step, a
    page, a re-plot), at a hover's or a click's change, after a menu item, after `Refresh()` and when a mod setting
    changes or a mod is disabled (`ModHost.Changed`). Every running mod's marks are drawn, in load order: first every
    `AutomapMark.Tint(x, y, colour)` (the square's inside - map pixels 1 to 13 of its 15, inside its edge lines - at
    60%), then every `Frame(x, y, colour, bold)` (a ring two map pixels thick, three bold, just inside the edge lines,
    with a one-device-pixel dark line inside it so it reads on grey and on black), then every `Arrow(fromX, fromY, toX,
    toY, colour)` (middle to middle on the level shown, outlined dark, its width and head growing with the zoom). They
    are drawn by the game (`AutomapOverlay`, laid exactly over picMaze) on the map's own squares at the size the map is
    drawn: a square's edges are the device pixels `AutomapLayout.DeviceEdges` gives the map's bitmap for the same zoom
    and display scaling, so the marks are as crisp as the map at 1x, enlarged and at 125%, and a resize being dragged
    stretches them with the bitmap until it is drawn again. A square's walls and doors are never covered.
  - `AutomapHover` (`(view, square or null)`): the pointer came onto another square of the map, or off it; told once
    per change.
  - `AutomapClick` (`(view, square) => acted`): a left click on a square of the map; every running mod is told. The
    original does nothing with a single click on picMaze; a double click still resizes as `picMaze_DblClick` does.
  - `view.PickSquare(prompt, done)`: the prompt takes the status line's place (lblInfo, "You are at ...") and the
    pointer over the map is a cross; the level bar, its arrows and < > still page the map; the square under the pointer
    is framed white; the next left click on a square of the map answers `done(square)` (the click goes no further), and
    **Escape** - from any window of the game, through the frames' key handling (`AutomapKeys.CancelPick`, checked only
    while a pick waits, so with no pick Escape is the game's as before) - or a right-click anywhere on the form (which
    then opens no menu) answers `done(null)`. One pick at a time: a second answers the first with null. `done` is
    guarded for the mod that asked, and not called once that mod has stopped running. On a form shorter than full size
    the picture lies over the level row (modAdjustMap), so the prompt is hidden there; < and > still page the map.
  Every one of these calls into a mod is guarded: a throw disables the mod, and the Automap carries on with what the
  other mods give, or the original's.
- **Actions** (moments the mod may act, each awaited by the game before it goes on; null = nothing happens there):
  `EnteredDungeonFromTown` - the party has gone down from town (the city's Dungeon picture or d), raised once the
  dungeon is up and before its clocks start; not for a character loaded below ground, nor after a raise below.
  `LeavingByStairs` - the stairs up out of level 1, after a won round is finished and before the dungeon closes (so
  before the town's rest and the `TownArrival` decision); not a rescue, not Exit to Main Menu. Each is a
  `Func<DungeonContext, Task>`; the live mods' hooks run in load order, one after another, each with a context of
  its own. With none hooked, or a hook whose task is already finished when it returns (no real await - Kaizen's
  casts are such), the game goes on at once exactly as without mods. A hook still running when it returns holds
  the dungeon's clocks (entering) or the climb (the stairs) until it finishes; the climb then goes on only if the
  party is still on those stairs, the dungeon still up and the one in command alive and not held fast. A hook that
  throws, or whose task faults, disables its mod as any other. `DungeonContext`: `Party`/`Members` (in order:
  hits, spell points, guilds, packs), `Spells` (MDATA2), `Item` (MDATA3 by id), `MonstersPresent` (live),
  `KnownSpells(c)` (`Spellbook.Known`), `Say(line)`, and two commands that run the dungeon's own path for a named
  member with the "Cast on Who?" answer given beforehand: `Cast(caster, spell, target?)` - the sheet's Cast
  (`CastKnownSpell`: `CastSpellHere`, i.e. `CheckCast` and then `CastAndThrow`, the sound, the save, the cast's
  line) - and `UseItem(caster, slot, target?)` - the Use button (paralysis refuses silently, `ItemUse.Problem`'s
  refusals said, then `UseItemSpell`: the spell at the item's level, the charge only when it went through). Both
  return whether the cast went through. The target is used only by a spell that asks (a party-wide one asks
  nobody, by hand too). Both refuse - false, nothing done, nothing said - while monsters are present (it would be
  a combat order), for a caster not in the party or dead, for a spell that asks and no target in the party, for an
  item not known at All (refused before it is looked at: below All the game shows nothing of its spell), for an
  item holding no spell, and for any spell outside `SpellThrown`'s effects (map spells, raises, binds, locks ask
  their own questions and stay the player's). A command stands for "switch to the caster and cast by hand": every
  line, refusal and sound reads exactly as if the caster were the one in command - the refusals (`CastGate`) in
  the "you" wording, never the one for another party member, and the closing line (`SpellThrown.Line`, given the
  caster as `commander`) naming the caster as the thrower, never the leader or the one in command. Once its hook has finished
  the context is closed and both commands refuse, so a mod cannot keep it to act later. `LastRefusal` is the game's refusal
  (`RefusalKind`) of the last command, if its gate refused it - what was just said aloud, e.g. `AntiMagic` - or null.
  One moment at a time: while a moment's hooks are awaited the stairs taken again do nothing, and the host runs no
  hooks of a second call made meanwhile (`ModHost.ActionRunning`). On the Mods page, Enter in a list cell keeps the
  edit and does not press Close.
  Underneath: `CastSpellHere`, `CastAndThrow` and `UseItemSpell` take an optional caster and preset target; a cast
  by hand passes neither, and runs as before. `CheckCast` takes `asCommander` (the "you" wording for any caster)
  and `SpellThrown.Apply`/`Line` an optional `commander` (null: `party.Selected`); a cast by hand is the one in
  command casting, so both give what they always gave.
- **The fight's round** (API version 14): `ModActions.CombatRound`, an `Action<CombatRoundContext>`, called at
  each tick of an engaged fight (timCombat, FRMDUNM.BAS:616; the round, MULTIMOD.BAS:497) before any character
  acts - not awaited, not for monsters only waiting, not for the free round a step gives them. The live mods'
  hooks run in load order, each with a context of its own, closed as its hook returns. With none hooked
  (`ModHost.CombatRound`) no context is made, nothing is read and the round is the original's. A hook that
  throws disables its mod. `CombatRoundContext`: `Party`/`Members`, `Spells`, `Item`, `Fight` (a token, the same
  object every round of one fight - never the engine, which holds the monsters' hits), `Groups` (the monster list
  as shown: `MonsterGroupView(Slot, Name, Count, Singular)` - the slot 0-3 a number key aims at, fn0E23's name for
  the one in command, the count only where the list shows one, and the same name for one; never hit points, never
  what an unidentified or unseen group is), `KnownSpells(c)`, `OrderOf(c)` (`CombatOrderView`: the order kind, its
  spell, an item order's slot, and `OpeningChest`, the Option column's "Open") and `AimedAt(c)`; and the commands a
  player gives between ticks for a named member, standing for "switch to them and press it": `Order(c,
  None/Fight/Defend)` (space, f, d - d refused for one held fast, as the key; a chest count called off first, as
  the keys do), `Cast(c, spell)` (the spell made the order as a spell buffer fires it, the chest count called off,
  then mdrcharCheckCast, whose refusal is said in the "you" wording and leaves what it leaves), `UseItem(c, slot)`
  (an item known at All holding a spell made the order, after sub15CD's guards, the check at the item's level and
  sub0BDE, as by hand; below All refused before it is looked at) and `Aim(c, slot)` (a number key). Each returns whether it went
  through; each refuses once the hook has returned, once the fight is over or another begun, and for a member
  not in the party or dead. Nothing is thrown or used there and then: the round plays the orders.
- **Settings**: `Get/Set(key)` for booleans (default from the declaration), `GetText/SetText` for strings,
  `GetRows/SetRows` for a list setting (mod.json `tables`), `GetChoice/SetChoice` for a choice setting (mod.json
  `choices`, API version 10), `Changed` (one of the mod's settings changed, with its
  key - for a window that shows one; a throwing handler is ignored),
  `DeclareToggle` for a toggle declared from code (shown only while the mod is loaded). Read them in your hooks rather
  than once at Load, and they take effect live.
- **Game** (API version 3): the game in play, read live on the UI thread, for a mod's own window - `Party` (null on
  the main menu, before a game and after one), `Selected` (the one in command), `Spells` (MDATA2), `Item` (MDATA3 by
  id), `KnownSpells(c)` (`Spellbook.Known`), and `Changed`, raised when a game begins or ends (the city sets it as it
  opens and clears it as it closes; a dungeon started on its own, `MORDOR_TEST=dungeon`, sets it too), when another
  character takes command (with `CharacterSwitched`), and whenever the Party Information window is drawn again (a
  member joining or leaving, but also every blow): a handler should redraw only what has changed. It hands out the
  game's own objects; a mod changes the game only through its hooks. A throwing handler disables its mod.
  `AskCastOnWho(spell, over)` (API version 4): the game's own "Cast on Who?" box - the list the dungeon shows for
  a cast by hand (`PickList`, frmInteract's font), titled `Cast "Spell"` with the prompt `Cast on Who?` and the
  party's names in order (never companions) - and the member picked, or null for Cancel (also with no party, or no
  window to show it over: headless, the probes). Nothing is cast; the answer is the mod's to keep. `over` is a
  control of the mod's own window (typed as object); the box is a dialog of the game's frame that window is a child of
  (`ModWindows.FrameOf`), so the keyboard goes back to the game when it closes. Call it on
  the UI thread, and never from inside a drop handler (post it: the drag's own loop is still running). The host
  supplies the box through `ModHost.CastOnWhoBox`, set at start-up (`ModStartup`).
- **Drags** (API version 4): the drags that set the spell buffers - a line of Objects & Spells' Spells list, or of
  its pack, in town and in the dungeon - carry, beside the game's own payload, a `ModDragged(Kind, Owner, Index)`
  under the data format `ModDragged.Format` (`"mordor/mod-dragged"`): `Kind` `Spell` or `PackItem`, `Owner` the
  character whose list it is (the one in command), `Index` the spell's place in `ModGame.Spells` (`SpellIn(spells)`)
  or the pack slot (`Slot`: null for the Hands and an empty slot, which are dragged too). A mod's window takes it with
  Avalonia's drag and drop: `DragDrop.SetAllowDrop(control, true)`, then in `DragDrop.DragOverEvent` set
  `e.DragEffects = DragDropEffects.Link` when `e.Data.Get(ModDragged.Format) is ModDragged`, and read it again in
  `DragDrop.DropEvent`. The game's own drop targets (the buffers, the pack, the Store's fields, the party's names)
  read only their own payload and are unchanged. It names the line, nothing more: what the player was shown of it
  is the mod's to respect - an item's `Slot.Known` before anything else about the item (the Kaizen window refuses an
  item below All before looking at it).
- **DataDirectory**: a folder for the mod's own files - the saves folder, where settings.ini is (so `MORDOR_SAVES`
  moves it). Name a file after the mod (`kaizen.json`).
- **Log**: `Info` into `mods.log` beside settings.ini (in the saves folder, so `MORDOR_SAVES` moves it), begun
  afresh at each start; `Error` there and in `crash.log` beside the saves folder (docs/GAME_FILES.md, "Files the port writes").
- **Harmony**: a `HarmonyLib.Harmony` with the mod's id. Patches made during Load and OnEnable are logged by method.

### Switching on and off on the main menu (`IModLifecycle`, API version 7)

Mods are switched on or off only on the **main menu's** Mods page, where no game window is open. There, ticking a
mod's **Enabled** box takes effect at once, both ways, with no restart, for a mod whose entry class also implements
`IModLifecycle` - as the bundled mods do (2026-10-24: the user found the restart unintuitive, then chose this
simpler design). A mod without it is switched at the next start, as before, and its status line says so. A game
begun afterwards builds its windows' mod buttons, columns, marks and windows from the mods running then, as it
always has; nothing already built has to follow a switch. On the in-game Mods page (the Control Panel's **Mods...**)
the Enabled boxes are greyed, with the note "Mods can be switched on or off at the main menu."; each mod's own
settings (toggles, list settings) stay changeable there as before.

```csharp
public sealed class Entry : IMod, IModLifecycle
{
    private IModContext _ctx = null!;
    public void Load(IModContext ctx) { _ctx = ctx; ctx.Events.MessageLogged += OnLine; /* every hook, once */ }
    public void OnEnable() => _ctx.Harmony.PatchAll(typeof(Entry).Assembly);   // patches here, not in Load
    public void OnDisable() { /* flush a file, stop a timer of your own */ }
}
```

- **Load** is called once in the session: at startup for a mod enabled then, or the first time it is ticked on on
  the main menu (its assembly is loaded then, in its own load context). Wire every hook here. Load is never called a
  second time, so nothing is subscribed twice.
- **OnEnable** follows Load, and is called again each time the mod is ticked back on. Make Harmony patches here.
- **OnDisable** is called each time it is ticked off. From that moment the host calls nothing of the mod - no event,
  decision, action, UI slot, window or tooltip: every dispatch asks whether the mod is running (`LoadedMod.Running`:
  loaded, `Active` and not faulted) - and after OnDisable it removes every Harmony patch made with `ctx.Harmony`
  (`UnpatchAll(id)`). The assembly stays loaded (one cannot be unloaded safely); a mod ticked back on is the same
  object, made active again, its patches put back by OnEnable. A mod window still open closes with its mod
  (`ModWindows.CloseStopped`; none is open on the main menu, which has no frame), still wanted open, and opens again with the next game if the mod runs then.
- **The one exception: `CharacterDeleted`** reaches every loaded mod, switched off or not (not one disabled for
  throwing), so a mod that keeps something per character (the Kaizen buff lists, the DPS Meter's tallies) lets a
  deleted character's go, and a new character of the same name does not take it over when the mod is ticked back on.
  A mod that was never loaded this session cannot hear it.
- The host: `ModHost.Switch(id, on)`, called by the main menu's page only, writes the Enabled flag to settings.ini
  and returns `ModSwitchResult.Now`, `NextStart` (no lifecycle, or disabled for throwing this session) or `Failed`
  (it threw in Load or OnEnable, and is disabled for the session). To tell whether a mod not yet loaded has the
  lifecycle its entry object is made (its assembly loaded, its constructor run) but Load is not called; that happens
  only there, on the main menu, and the object is kept for the next tick. `ModHost.StatusOf(id, enabled)` is the
  page's line, shown only when it says more than the Enabled box (`ModsWindow.IsProblem`): never "Running." or
  "Off.", but "Starts the next time Mordor starts." or "Running now; off from the next start." for a mod that
  must wait, and a load failure or a fault. Each mod's group is its name and version, its one-line description,
  the Enabled box, that line if any, and its settings, each with at most one short line of description. A mod loaded on the main menu takes its place among the
  others in the order they were found, so its buttons and columns stand where they will at the next start.
- Saves: the tag (`SaveTag`) is the mods running when the character is saved; the "saved with mods that are not
  running" box compares a save's tag with the mods running when it is loaded.

### What the game guarantees

- Nothing loads unless enabled on the Mods page; enabling or disabling, on the main menu's page, takes effect at once for an
  `IModLifecycle` mod (above), and at the next start for any other.
- Every call the game makes into a mod is guarded: Load, OnEnable and OnDisable, each event and decision hook (a `TownArrivalAnswer`'s line
  included), an Objects & Spells command's visibility test and Run, a Bank command's visibility test and Run, a party
  column's visibility test and cell, a pack-line or Bank-line decoration, building a mod window, the Automap hooks
  (`AutomapMenu`, a menu item's Run, `AutomapMarks`, `AutomapHover`, `AutomapClick`, a pick's answer), and each Action
  hook (`EnteredDungeonFromTown`, `LeavingByStairs`: a throw or a faulted task alike; `CombatRound`). A mod
  that throws in one of those is disabled for the rest of the session, its Harmony patches are removed, the
  exception goes to the log, and the game gives the original's answer.
- What the game does not call is not guarded. The event handlers of controls a mod builds itself (a button's Click
  in its own window, say) and the bodies of its Harmony patches run as ordinary code: an exception there is not
  caught by the mod system, and is handled, or not, as any other exception in the port would be. An exception
  escaping such a handler can end the game - an `async void` handler above all (a Copy button's awaited clipboard
  write, say), where it reaches the dispatcher. Catch in every handler of your own controls and report through
  `ctx.Log.Error`, as the Quality of Life mod does (`QolMod.Report`, `Views.Safe`).
- Saves record the running mods; loading a save that names a mod not running shows an OK box once, then plays.
- The text the game draws for a mod - a Store column's cells, a party column's, the Store check box, an
  Objects & Spells or Bank button, the toolbar's window buttons, a mod window's frame - is in the Options window's
  "NonPro" font, the face and size of the list or form it sits on (docs/FIDELITY.md, "The Options fonts are
  applied"). A Store column's heading is in the "Pro" font, the Courier New of the Store's own "Item" and "U G N E"
  headings on the same line, and is set in as far as its cells. A control a mod builds itself inherits that font
  from where it is put unless it sets one of its own.

## The design (agreed 2026-09-26)

## Principle

The game itself stays the faithful port of the original. Everything that changes how it plays or looks is a mod:
opt-in, layered on top, and off by default. With no mods loaded, the game is the original, and every fidelity
check (`tools/check.sh`, `docs/FIDELITY.md`) runs with mods off, so a mod can never quietly change the faithful
default.

## What a mod is

- A folder under `mods/` holding a C# class library (`.dll`) and a small manifest (name, version, author, the game
  version it was built for).
- The game loads each **enabled** mod at startup and calls its entry point with a *mod context*, its handle on the
  game. A mod that implements `IModLifecycle` is also loaded, switched off and on again from the main menu's Mods page, without a restart (API version 7).
- Mods are turned on and off in a **Mods** list (the main menu's and the in-game Control Panel's Mods buttons; first built inside Options), and the choice is kept in the port's `settings.ini`.
- **Language: C#.** Mods use the game's own types (`Character`, `Item`, the combat engine), are compiled and
  type-checked, and fail at load rather than mid-game when the game changes. A Lua layer over the same hooks can be
  added later if non-programmers need it.

## Layer 1: named hooks (the stable API)

The game fires hooks at chosen points. They keep working when internals change, because their names and meaning are
the contract.

| Kind | What a mod can do | Example |
|---|---|---|
| **Events** | Observe and react; cannot change the outcome | `ctx.Events.MessageLogged`, `CaptionShown`, `BlowLanded`, `HitsChanged`, `FightStarted/Ended`, `StoreRowClicked`, `CharacterSwitched` |
| **Decisions** | Answer a question the game asks; the default answer is always the original's | `ctx.Decisions.StoreAllowsRowChangeWhileChoosing` |
| **UI slots** | Add controls to a window, or add a window | `ctx.UI.PartyRowExtras`, `ctx.UI.AddWindow("Combat Log", …)` |

```csharp
ctx.Events.MessageLogged += line => myLog.Add(line);
ctx.Decisions.StoreAllowsRowChangeWhileChoosing = () => true;
ctx.UI.PartyRowExtras += row => new HpBar(row.Character);
```

The limit: a mod can only touch what has a hook, so a new kind of mod may need a new hook added to the game. That is
usually a few lines.

## Layer 2: Harmony (patch any method)

[Harmony](https://github.com/pardeike/Harmony) patches the game's .NET methods at run time, as RimWorld and Stardew
Valley mods do. No hook is needed.

- **Prefix:** runs before a method; can change its arguments or skip the original.
- **Postfix:** runs after; can read or replace the return value.
- **Transpiler:** rewrites the method's instructions. The most powerful and the most fragile.

```csharp
[HarmonyPatch(typeof(StoreStock), nameof(StoreStock.PriceOf))]
class HalfPrice { static void Postfix(ref long __result) => __result /= 2; }
```

It is the escape hatch for anything without a hook. It is tied to internal method names and structure, so patches can
break when the game changes. A patch that proves popular should become a named hook.

## Changing the GUI

| Level | How | Stability |
|---|---|---|
| **1. Restyle** | A mod ships Avalonia styles/themes (colours, fonts, borders, button templates, spacing) loaded at run time, plus replacement art and icons. Often no code at all. | Stable |
| **2. Add** | UI slots: add controls to existing windows (an HP bar in the Party window) or whole new windows (a combat log). | Stable (named hooks) |
| **3. Replace a window** | A per-window hook lets a mod supply its own version of a window (a redesigned Store or Party panel); the game still provides the data and actions. | Stable once the hook exists; hooks are added window by window |
| **4. Change anything else** | Harmony patches on the code that builds a window, to move, hide or change individual controls. | Fragile |

Caveat for levels 3 and 4: the port's windows are rebuilt from the original's forms, often assembled in C# rather than
layout files. Deep GUI modding would benefit from naming the key controls consistently, and from moving the big windows
into separate layout files that a mod can override. Do that window by window, as needed.

## Safeguards

- Every loaded mod, and every Harmony patch, is written to `mods.log`. Problems - a mod.json that cannot be read, a
  mod that fails to load or is disabled for throwing, a game-version mismatch, a mod's own `Error` - go to `crash.log`
  as well; a start whose mods load cleanly writes nothing there.
- A mod that throws in a call the game makes into it is disabled for the session; the game carries on. Its own
  controls' event handlers and its Harmony patch bodies are not guarded (see "What the game guarantees").
- Saves record which mods were active, and warn when loaded without them, naming each by its mod.json name when it
  can still be found ("Quality of Life 1.0.0") and by its id when it cannot.
- Mods are ordinary code with full access to the PC: install only mods you trust.

## Later: data and asset mods (no code)

- **Art and sound:** a mod's images, sounds and music replace the originals by file name.
- **Data tables:** exported to JSON; a mod supplies changed or added rows (monsters, items, spells, guilds, races,
  store stock). The original data files are never modified.
- **Maps:** need an editor; the original's own editor form (FRMEDITC) is the natural starting point.
- **Rule settings:** selected constants exposed with the original's values as defaults. Handle case by case, because
  many are tangled into the original's formulas.

## First mods planned (the user's requests)

1. **Unlocked Store alignment choice.** *Built, in the Quality of Life mod.* While Select Alignment is up, clicking or
   arrowing to another item switches to it; the list, search and EXIT stay usable.
   - It was built into the game (commit 164befa) and recorded in FIDELITY.md as a deviation made at the user's request.
   - **Done as agreed:** the game's default is back to the original's lock (FRMCTYS.BAS:332-335, 358, 981), and this
     behaviour is in the mod.
2. **HP and SP bars.** *Built, in the Quality of Life mod (Party window; not the character window).* Bars showing the percentage of maximum hits (green to red) and spell points (blue to purple), in the Party window
   (and possibly the character window).
3. **Extended combat log.** *Built, in the Quality of Life mod (1.0.0 to 1.8.0):* the Combat Log window with every caption and Messages line, and Messages kept to 200 lines. Removed in 1.9.0 at the user's request, in favour of Color-coded combat messages (the Messages window's combat lines coloured by kind).
   - A longer Messages history: the original keeps 20 lines (mc04F2).
   - Optionally a separate window that also logs what the original shows only in its hit captions (hits, misses,
     crits, backstabs, damage taken, spells).
   - Default content, unless the user decides otherwise: everything shown in the captions.

4. **Toolbar tooltips.** *Built, in the Quality of Life mod.* Mouse-over labels on the toolbar buttons with their function key, for example
   "AutoMap (F8)" and "Control Panel (F10)". The original (VB3) has none, and the port's own were removed as
   untraced, so this belongs in a mod (a UI-slot mod on the toolbar).
5. **Experience bar.** *Built, in the Quality of Life mod (the XP bar, behind the Next column).* In the Party window, a bar per member showing progress towards the next level, or towards the
   pin point (where experience stops counting until the character levels) when that comes first. The thresholds must
   come from the game's own rules (the guild's level table and the pin rule), not be recalculated in the mod. A UI-slot
   mod on the party rows, like mod 2. (The "Next" column, 7 below, is the text half of this.)
6. **Item sort.** *Built, in the Quality of Life mod.* A Sort command in Objects & Spells: the pack by item type, then
   name, with Hands fixed in slot 1, empty slots last, and buffers following their items as a swap moves them.
7. **XP to next level column.** *Built, in the Quality of Life mod.* A "Next" column in Party Information with the
   experience still needed for the next level (the toolbar's figure); once earned, the amount still needed to the
   pin (since 1.3.1; "Ready" before); "Pinned".
8. **ALT-p pools and deposits (town).** *Built, in the Quality of Life mod.* In town ALT-p does the Bank's Party Pool
   & Deposit; in the dungeon it stays the original's on-hand pool.

## Build order and rough effort

1. The mod loader, context, Mods list (first in Options, now the main menu and the Control Panel), save tagging and safeguards: about 1–2 days of agent work.
2. The first hooks: the Store choice, hits/SP display, the message and combat streams.
3. The planned mods above, with the Store default restored to the original: about half a day.
4. An adversarial review, with each mod tested on screen both on and off.
5. Later: data and asset overrides (about a day), then maps and rule settings as needed.
