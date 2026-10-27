# Untraced text and screens - the work queue

An audit on 2026-09-25 of player-visible strings in Mordor.App and Mordor.Core with no counterpart
in decompiled-named/*.BAS, the help or the screenshots. Each item is to be traced and replaced by
the original's behaviour, or recorded in FIDELITY.md if it has to stay. Tick items off as they land.

## Character window
- [x] Stats, Resist., Char, Guild, Look pages (done)
- [x] Buffers: 1.-10. numbering, lblBufferStat cost column; dungeon buttons 1-10, no tooltips
- [x] Misc page (item, spell, companion all done): item info (MAINMOD2.BAS:5040-5168), companion (4934-5039), spell (4813-4933),
      painted with sub09B8, repainted live; replaces Loot.Describe / CompanionInfo / SpellInfo text.
      Store, bank and confinement "Info" should go to Misc too (FRMCTYS.BAS:165/185,
      Item page done (ItemInfoPage); spell page done (SpellInfo.Paint); companion page (4934-5039) remains.
- [x] Companion Info also records the creature in the library: sub06FE (MONSMOD.BAS:3938-3995) from picCompanion_DblClick (FRMCHAR3.BAS:756) - new entry with discoverer, Known raised by id level (0->4,1->3,2->2,else 1), place if in the dungeon
- [x] Confinement Info buttons: cmdIDInfo_Click and cmdInfo_Click (FRMCITYC.BAS:208-237) open the companion page (store path is CompanionInfo.Paint(null, -1, ..., button alignment)); not wired
- [x] Spells window Info: the original takes a typed name, any spell (fn1725, FRMCHAR3.BAS:282-302), writes the full name back, and on a miss refocuses; the port uses the selected known spell
- [x] mdrcharShowInfo 4808-4811: a minimised character window is restored and nothing is painted that time; the port paints and restores
      FRMCITYB.BAS:80, FRMCITYC.BAS:201/217/237).

## Frequently seen
- [x] DungeonWindow.axaml.cs ~2097: "You fall down a chute..." (chutes done; pits still roll only for the one on screen - next) -> MAZEMOD.BAS:131/134 "You have fallen into a chute for N hits."
      FULL PLAN (research done): per-member levitation roll (HoversOverPit), damage Rules.ChuteDamage at the new level (ToEven) unless levitating, messages for Me / others on the leader's level, Follow Leader Yes/No box (MAZEMOD.BAS:1853-1858) when the leader falls in a party, "There is a chute here.  Select TAKE to use." for Me when not falling, FALL then HIT sounds, depth 0 = no fall, lost flags per member; then sub0908: anyone off the leader's level gets "\"Name\" has disappeared from view!", killed if Hits<=0, removed and saved like AskToLeave. TAKE on a chute (MAINMOD2.BAS:3996-4009): everyone falls, no box, no sound. Pits should also roll and damage per member (1743-1777).
- [x] CityWindow.axaml.cs ~387-398 pack-row tags "(att X def Y)", "[n charges]", "(wrong guild)", "[WORN...]" - original marks with * and bold/italic
- [x] Character.cs ~989 "You must bring back one X." -> FRMCTYG.BAS:563 "You must return one X."
- [x] Guild screen body (CityWindow.axaml.cs ~845-903): guild list, "Make active", "Advance a Level", extra lines after level-up (FRMCTYG.BAS:223, 417)
- [x] ItemUse.cs ~141/144 "You gain 50/200 spell points." - original silent (MAINMOD2.BAS:6167/6172)
- [x] CombatOrder.cs ~88 "Spell - buffer n" - only a probe reads it, never the screen; nothing to do
- [x] Loot.cs ~76 "You can tell nothing more about it." - Loot.Describe removed with the Misc item page
- [x] Seer locate-character "Name somebody." / "I am unable to find anyone called" (FRMSEEK.BAS:326, 88)
- [x] Bank buttons "Deposit All" etc. vs bm7.png
- [x] Store "Equip / Unequip", "ID Cost", "Buy as", "(second-hand only)"
- [x] Party join/leave texts (MULTIMOD.BAS:753)
- [x] "You have nobody to exchange with." (FRMMENU.BAS:68-86), "X has no gold to give."
- [x] Confinement labels - folded into the Confinement rebuild item
- [x] "Dead on level N" combat panel title
- [x] "That is not a pair of coordinates."; prompt spacing (MAINMOD2.BAS:1120/1138)
- [x] "You cannot make it out at all." - all four sites silent now
- [x] "Pick a companion first." / "Choose a spell first." / "Pick something in the pack first." / "Pick a spell first." - silent now, guarded in selfcheck
- [x] Character creation "Stat Points Left", tooltips, Guild Stats legend
- [x] Title window / character picker texts (TitleWindow.axaml.cs)
- [x] Dead invented city character sheet (CityWindow.axaml.cs ~2033-2138, MORDOR_TEST_PAGE=character)
- [x] Dungeon .axaml captions ("Buffers (Shift+1..0 ...)", "Close (I / Esc)", "Stop (Space)")
- [x] Dungeon BufferBox and its item/spell/clear buttons with "Buffer N set." - the port's stand-in for dragging onto lblBuffer (FRMCHAR1.BAS:219-233), which CharacterPanel now does; remove
- [x] Give from the bank by dropping a vault line on a party member's name (MAINMOD2.BAS:2972-3002): "Cannot give Guild-Issued Items!", "X has no free slots!" / SORRY!
- [x] The guild's check for an issued item already in the vault (FRMCTYG.BAS:703-718)
- [x] Dragging an empty pack slot is allowed in the original (FRMCHAR3.BAS:589-593)
- [x] Store: "Equip / Unequip" (and Use/Drop beside it) and "Buy as"/"(second-hand only)" - original shows an alignment chooser (elaAlignments/cmdAlign, FRMCTYS.BAS:342-370) only when 2+ alignments are stocked, and the Cost label reads "<count>(g) Cost"
- [x] Seer's Monster and Item lookups: fn170D prompts and MsgBox answers now (the known-as monster name match, FRMSEEK.BAS:75-83, is still missing)
- [x] Dungeon combat panel Attack/Defend/Stop buttons: the original has none (toolbar Fight/Spell, keys)
- [x] Party window: bm5.png has only Switch To / Options / Leave; the port's Exchange and "Pool gold" buttons are not there, Leave shows even alone
- [x] DungeonWindow AskToJoin (~1685-1715) is still the port's decorated list; use NamePicker as the city does; password boxes MaxLength 10 (FRMINTER.BAS:465-467)
- [x] NamePicker: the original keeps the last picked index (m001C) and is a true drop-down combo
- [x] City party window lacks Switch To and Options buttons (bm5.png, FRMGROUP.BAS:32-46); the dungeon has them
- [x] Seer item search: when every draw repeats a carrier already named, the original box is cut short to "The creature(s) kno" (FRMSEEK.BAS:316)
- [x] StoreStock SellPrice / IdentifyFee / PriceOf truncate where the original's Long rounds half-to-even (FRMCTYS.BAS:584, 607, 750-751), and add Max() clamps the original lacks
- [x] Store: the Buy list's scrollbar can still be dragged while Select Alignment is up (the original disables vsbItems)
- [x] CityWindow.StoreRow is dead code since the store rewrite
- [x] Main menu: Change Password, Exchange Items (loaded only), Hall of Records, About... not ported (FRMMENU.BAS:39-40, 42-87, 116-117) - NOTE: the party window Exchange button is gone, so Exchange is unreachable in play until this is done
- [x] Main menu: "Unknown error N reading character!" shows the .NET HResult where the original shows fn04CD's return code
- [x] Character List / Load picker order by file name, not the roster's record order (port has no roster file)
- [x] Combat: the order glyph is not drawn while monsters wait unengaged; with monsters waiting, Cast casts at once where the original sets a combat spell as the order
- [x] Combat order glyph after F then Cast: the original tests Att before M56C4 and still shows "+"; the port has one order kind and shows ">"

## Second audit (rules and text), 2026-09-25
- [x] Experience is banked and paid only when the fight is won (Combat.cs:326-364, DungeonWindow ~4149); the original pays each blow at once (sub0F11 via sub0CDF, MAINMOD2.BAS:279-310, MONSMOD.BAS:468/495)
- [x] RestInCity does not clear SpellEffects (FRMCTYM.BAS:111), and returns early for the dead / "nothing to do" where the original runs the block for every member (107-134)
- [x] Chest item receiver: CInt(Rnd * UBound(Chars)) rounds half-even, ends get half weight (MONSMOD.BAS:3788); port draws uniformly
- [x] Pickup identification compares unrounded doubles (Rules.cs:796-810); fn12F0 stores Integers (MAINMOD2.BAS:2480-2499) - use Loot.ReadableLevel
- [x] Drowning: "{me}drowns." invented; original kills silently and shows "You are Drowning!" / "You are Sinking!" in lblSquareInfo (FRMDUNM.BAS:500-525)
- [x] AilmentTick order: original identify, water, poison, disease, paralysis, sub0C00, old age, Hits<1 death (FRMDUNM.BAS:449-584)
- [x] "Can make a level" announcement tests MaxLevel too (Character.cs:895); original only experience vs fn12A1 on the transition (MAINMOD2.BAS:297-305)
- [x] Fire trap truncates damage (ChestTraps.cs:88-91); Integer assignment rounds (MONSMOD.BAS:3530-3533)
- [x] Disease cure roll subtracts poisonLevel*2 even on the tick poison was cured, where Ailments(0) was already zeroed (FRMDUNM.BAS:538-551)
- [x] Monster slay-class spell on a companion checks the first living group, not Foes(CombatOrders(c).M547A) (MONSMOD.BAS:2561-2564)
- [x] StoreStock.PriceOf still clamps Count with Max(1, ...) (FRMCTYS.BAS:590 has none; Log(0) errors to price 0)
- [x] Breath from a guild-level-0 monster: port Max(1,...) guard; original Log(0) error ends that group's turn (MONSMOD.BAS:2708)
- [x] Combat monster list extras (companion rows removed; the HpText status line remains - see below) "with you: ..." companion rows and an HP/SP line (DungeonWindow ~4326-4329)
- [x] "Nothing there." (Party.cs:190) - original refuses slot 0 silently (FRMEXCHA.BAS:170-180)
- [x] "Welcome to the City Seer!" - false alarm: bm11.png shows it (a form caption, not in the code)
- [x] "_Visit the Morgue" city button caption/style; "{name} (your companion)" in morgue lists - caption done, colours gone; companion rows moved to the Pickup item
- [x] Item pickup line "\"taker\" - Slot #n, item" (DungeonWindow ~4064) - check against MONSMOD.BAS:3788-3800
- [x] "Something went wrong handling that key..." shown to the player (DungeonWindow ~251)
- [x] Options window captions vs FRMOPTIO.BAS; "Not built yet." tooltip
- [x] "Game Options..." (help: "Game Options"); "Locate Options" is right - bm11.png shows it
- [x] Confinement "In the pens" / "(the pens are empty ...)" text - folded into the Confinement rebuild item
- [x] MainWindow / HelpWindow shell text ("Data folder:", "New Character") - MainWindow opens only under MORDOR_TEST=browser (a dev tool); HelpWindow queued separately
- [x] Not yet audited: encounter generation in detail; player spells beyond damage; uncurse/realign/combine/stock; stat loss in mdrcharAddAge
- [x] ExpForNextLevel / ExperienceCap restate fn12A1 / fn13ED algebraically; ~0.2% of cases are +-1 from Currency rounding (MAINMOD2.BAS:2118-2137, 2710-2712); Sim.cs averages experience over won fights only
- [x] The dungeon HpText status line ("HP x/y SP ... Gold ... Exp ... ⚠ ailment") has no original counterpart; the original has lblHits/lblSpells etc. in its own windows
- [x] StoreStock.IsStarter adds Price>0/Type!=34/Id!=0 to the original Floor=0 early return (FRMCTYS.BAS:594); a Type-34 floor-0 item should return its list price.
- [x] Loot.ReadableLevel skill uses EffectiveStat (min 1, StatModWorth alignment); mdrcharGetIDSkill adds Stats + StatMods plainly (MAINMOD2.BAS:2511-2512) - check what StatMods holds
- [x] Paralysis wear-off in AilmentTick is gated on _combat != null; the original tests Ailments(2) > 0 alone and always passes with no foes (FRMDUNM.BAS:552-561) - where does the port keep paralysis outside a fight?
  - plan (researched): Character.ParalysisLevel saved (Ailments(2)); sub0D1D strips the stat part of Att/Def while paralysed (MONSMOD.BAS:3884-3906); persists after a fight until the tick (always passes with no foes) or the companion-kill cure (MONSMOD.BAS:1029-1033); cure spell effect 10 cures the caster (MAINMOD2.BAS:917-928); cleared by sub048C on city entry and death; blocks arrows (MAINMOD2.BAS:3766), stairs (3953), pickup (3655), drop body (3690), give (3707)
- [x] Kill does not prune when nobody is able (e.g. the rest stoned); the original UPDATE prunes whenever UBound > 0 (FRMGROUP.BAS:326)
- [x] Confinement page is the port's own layout ("Your companions", "In the pens", "(no companions)", pens-empty line); rebuild against bm9.png (groups Bind Companions / Identify, Realign & Sell / Buy Companions; "NO COMPANIONS!" row, FRMCITYC.BAS:506-508)
- [x] Pickup (p key, morgue Grab Body) is a typed name via fn170D "Pickup Character" / "Character's Name?" with FRMINTER.BAS:153-252 errors; the port lists names and adds "(your companion)" rows (DungeonWindow ~3335, CityWindow ~1099)
- [x] Options Backup / Restore (FRMOPTIO.BAS:260-280) and the font "..." pickers (285-297) are disabled stand-ins
- [x] HelpWindow title/header: the help file's own |SYSTEM title, WinHelp's caption - "M1: Depths of Dejenol Help Lesson" in the full 1.1, "Mordor HelpLesson" in the shareware (observed in the original shareware 2026-10-26); it had been the full help's Welcome heading, "Mordor 1.1 (M1 Depths of Dejenol) Help Lesson", for both. The search chrome has no source
- [ ] "Game Options..." access key: hidden by the focus box in bm0.png - leave until a clearer capture
- [x] Title window (frmMenu) shows an empty window caption; check what the original caption was (no main-menu screenshot yet). The shareware's frame reads "Mordor 1.1 (Limited Version)" at the menu (observed in the original shareware 2026-10-26), which the limited build now shows; the full game keeps the empty caption
- [x] Companions are a List; selling leaves no hole where the original sets Ref=-1 (FRMCITYC.BAS:276-300); companion drag source for the Comp. boxes (FRMCITYC.BAS:744-796)
- [x] Confinement: the Search for What? box overruns the Buy Companions frame on the right
- [x] Paralysis A/D faults: the original stores Att/Def, so a city-gate clear (FRMCTYM.BAS:113-115 before sub048C), a level-0 paralysis, or stat/gear changes while paralysed leave a permanent A/D shift; the port computes A/D and cannot show it - would need a stored A/D offset on Character
- [x] Morgue page vs bm10.png (FRMMORG.BAS): "Welcome to the Morgue!" label, Raise Character / Hire Rescuers / Grab Body / "Raise Companion #n for X" / Exit; the port's page is its own layout
- [x] mdrcharGetCarry (MAINMOD2.BAS:2027-2078): dead companions stay at 0 hits and the heaviest is carried ("dropped X to carry Y", "was too big to carry"); the port removes a dead companion at once (MonsterAttack.cs CompanionFalls)
- [x] City: some sub085B lines go to MsgLine (one line, overwritten) instead of Log (the Messages list, which keeps every line); audit every MsgLine.Text assignment against sub085B vs a label
- [x] Monster group loop ends on !Party.AnyAble; the original leaves only when UBound(Chars) = 0 (a solo party) (MULTIMOD.BAS:364)
- [x] GetCarry selfchecks still missing: "dropped" with a companion carried, "dropped" then "too big" keeping the old carry, and the "Name, you are now carrying X." wording
- [x] MessageList.Add drops a line equal to the one above; sub085B does not (MAINMOD2.BAS:3195-3208). Check for port double-logging before removing the guard
- [x] Dungeon: the full-pack give refusal ("X has no free slots!") shows in MsgText; the original is a SORRY! box (MAINMOD2.BAS:2953) - DungeonWindow ~912, ~1231
- [x] Character has no stored area (M59BB); the Exchange location check computes it from X,Y, which differs for a rock death (M59BB = False, MAINMOD2.BAS:1185)
- [x] First-run "Welcome to Mordor 1.1!" box and the window close-button "Please use the Mordor EXIT buttons" message are not ported (would write MORDOR.INI)

## Audit 2026-09-26: encounters, store, aging (area B, non-damage spells, still to audit)
- [x] Encounters: companion groups 2-4 use an invented +-1 floor / same-kind rule (Combat.cs ~1832-1843); fn0D4D draws CompanionType by CompanionSpawnMode 0-7, Subtype match dropped after 150 tries, chained off the previous companion (MONSMOD.BAS:1727-1847)
- [x] Encounters: the port can stop short of the group count (Combat.cs ~1839); the source re-rolls and fills every group (MONSMOD.BAS:1781, 1850)
- [x] Encounters: class pick is Rnd*101 over the class table first, with the loop's v(0)-for-v(k) fault and ~1% overflow to the last class (MONSMOD.BAS:1542-1555); the port weights all classes together (Combat.cs ~1799-1806); the 1% any-class roll is uniform (MONSMOD.BAS:1470-1471)
- [x] Encounters: level-1 newcomer protection missing - area index < 18 on level 1 suppresses the 1% roll and re-rolls GuildLevel > 2 except monster 4 (MONSMOD.BAS:1465-1470, 1646-1648)
- [x] Encounters: KnownAs is set once per group (MONSMOD.BAS:1882-1946), m02AE Integer-rounded (1913); the port rolls per monster with a double (Combat.cs ~1862, ~1910)
- [x] Encounters (rare): join offer cleared whenever more than one group (MONSMOD.BAS:1948); hits may roll 0 (1873-1878, port Max(1)); lair filter adds Hits > 0 (port only, ~1787); retry cap 10000 with "MONGEN:piTry>10000" (1733-1737); no-fight square bit at 1432; EntryAreaId "arrival room holds nothing" has no source in fn0D4D - find its caller or remove
- [x] Store: combining zero-charge items of Kind 23-28/30/31 is allowed in the source (FRMCTYS.BAS:662-668); Combining.cs ~35 needs Charges > 0
- [x] Store: starting stock tests Items.Align > 1 (MAINMOD.BAS:894); StoreStock.cs ~247 tests more than one alignment bit - check the data for items that differ
- [x] Store (rare): combined charges past 32767 overflow in the source (FRMCTYS.BAS:677), the port caps; combine clears the whole slot where the source only sets Ref = 0
- [x] Store: buy and sell add to ItemsSeen (CityWindow.Store.cs ~670, ~694) - no source; FRMCTYS only on identify (145) and uncurse (312)
- [x] Store: citation drift - comments cite old decompiled/ line numbers (CityWindow.Store.cs ~539 MODULE6.BAS:1745 -> MAINMOD2.BAS:1749-1751; StoreStock.cs, Combining.cs, Uncursing.cs)
- [x] Aging: Sanctuary ages 365 days (mc0282, MAINMOD2.BAS:95, 803, 1352); SpellEffect.cs ~50 has 28
- [x] Aging: mdrcharAddAge's "You are now X older." / "\"Name\" is now X older." (MAINMOD2.BAS:6401-6405) is never printed - Sanctuary, monster aging (MONSMOD.BAS:3129-3137), chest Withering (3633), raises (MAINMOD2.BAS:363, 1663, 1680, 3109)
- [x] Aging: a stat item of Kind 25 ages 28 days with the line (MAINMOD2.BAS:6228); ItemUse.cs ~156 does not
- [x] Aging: the rescue "N week(s)... older." text has no source - FRMDUNM.BAS:189 passes False, and the found text is a MsgBox (DungeonWindow ~2567)
- [x] Aging: dead members are rested too (FRMCTYM.BAS:107-110, MAINMOD2.BAS:6355-6360); Character.cs ~661 returns early for the dead and skips the replenish when member 0 is dead

## Audit 2026-09-26: non-damage player spells (mdrcharCastSpell, MAINMOD2.BAS:855-1385)
- [x] Spells from a buffer skip mdrcharCheckCast (MAINMOD2.BAS:830-835 via fn1521 4159): no SP check, no antimagic refusal, no level gate (DungeonWindow ~3394)
- [x] Buffered map spells (Teleport, Displacement, Passwall, Detect Rock, Depth Perception, Find Direction, Soul Search, Fetch, Sanctuary, raise, open, bind) fall into CastHealOrCure when CastUtility returns null (DungeonWindow ~3395); should go to CastHere. Spells.cs CastUtility/ApplyToCharacter and HealOutOfCombat are then dead duplicate code
- [x] Spell resistances survive the city: sub048C zeroes SpellResistances on city entry (FRMCTYM.BAS:130, MAINMOD2.BAS:4369-4371); RestInCity only clears SpellEffects
- [x] Cure Poison/Disease and the Cure heal clear Poisoned/Diseased but leave PoisonLevel/DiseaseLevel, so MonsterAttack (~618, ~623) never poisons/diseases again until the city; the source has one field Ailments(0)/(1) = False (MAINMOD2.BAS:976-977, 993, 1000)
- [x] Levitate/Sight Veil/See Invisible/Protection call Effects.Recompute (SpellThrown.cs ~134); the source only sets Ailments(n) and the bit (MAINMOD2.BAS:1008-1047) - Levitate grants See Invisible at once, and casting 3-5 resets spell Protection to the guild value
- [x] Depth Perception / Find Direction do not clear LostLevel / LostFacing (MAINMOD2.BAS:1263, 1272, then sub0848 and LookAround); DungeonWindow ~1619-1625
- [x] Detect Rock writes the automap: the rock bit ahead unless lost, else bit 23 "not rock" (MAINMOD2.BAS:1238-1256); DungeonWindow ~1627-1637 writes nothing
- [x] Ethereal Portal into rock kills the whole party with the rock death (8) and marks rock on the automap (MAINMOD2.BAS:1197-1213); the port kills the caster only, ordinary death
- [x] Retrieve Soul lists only characters dead in rock (DeathState 8) on your level with area 0 (FRMINTER.BAS:343-351; typed 178-190: "Character is not on this level!", "Character is not in ROCK!"); the port offers anyone
- [x] Locate list excludes only the caster, not the whole party (FRMINTER.BAS:380-382; DungeonWindow ~1436, ~1956)
- [x] Healing items heal at the item's spell level (MAINMOD2.BAS:946-950, 6140); the port uses level 1 (DungeonWindow ~1936, Spells.cs ~354)
- [x] An item with spell level 0 is charged spell points (MAINMOD2.BAS:1355 skips only when p01AA <> 0); the port treats every item cast as free
- [x] With monsters present, Teleport and Cure Paralysis go to the combat branch (MAINMOD2.BAS:856; FRMCHAR3.BAS:174-182); the port casts Teleport on the spot (DungeonWindow ~1369, ~3589)
- [x] Teleport/Displacement return _nav.Describe() (DungeonWindow ~1614); the source says nothing itself (MAINMOD2.BAS:1130-1134, 1147-1152) - check what mdrcharTeleport's sub0835 prints
- [x] A lone character with no companions gets no "Cast on Who?" box; fn170D always opens it (DungeonWindow ~1901)
- [x] SP are charged when Teleport/Displacement/Passwall kill the party; the source exits without charging (MAINMOD2.BAS:1126-1128, 1144-1146, 1223-1225)
- [x] An item spell with a map effect says "Nothing Happens." and uses a charge (DungeonWindow ~2059); it should cast the map effect
- [x] Effects 0/18 ask "Cast on Who?" only when M5B33 > 0 (MAINMOD2.BAS:931); SpellThrown.cs ~192 always asks
- [x] Comment fixes: SpellEffect.cs ~213-215 says a failed bind is free (it is charged)
- [x] Other ItemsSeen.Add callers overstate the Library (Absorb makes every entry grade 4): TakeFromVault (CityWindow ~1603, FRMCHAR3.BAS:550 sub0730), CityWindow.Store.cs ~546, ~586, ~670, ~694 - use Librarian.FileItem with the sub0730 grade
- [x] mdrcharAddAge "older" line (Character.AgeBy) still to wire for monster aging, chest Withering, the raises and Kind 25 items
- [x] OpenTheChest refuses a character on 0 hits (CurHits <= 0); the source tests only DeathState (MAINMOD2.BAS:4064)
- [x] An item used in combat is cast at once; check whether it should become the order like a spell (MAINMOD2.BAS:6129-6130)
- [x] Store each room's Foes and Fight record (fn0E38 load, fn0F88 write-back after generation, at sub0C53 and on walking away mid-fight; MAZEMOD.BAS:811) instead of regenerating from a seed
- [x] sub0C53 asks the Asmodeus question first (MONSMOD.BAS:770-774), before the stamp and the gold lines; the port asks it last (EndCombat and the join path)
- [x] The Asmodeus cue plays per draw alongside the encounter sound (MONSMOD.BAS:1649); the port plays it instead of EncounterSound (DungeonWindow ~2549)
- [x] ItemUse: VB calls mdrcharCheckItems(pv0850, True) just before the Kind 25 aging (MAINMOD2.BAS:6227); the port has no equivalent there
- [x] Store replenish "someone bought one" branch reads Align(l02C2) with l02C2 stuck at 0, so it only checks the Unaligned count (MAINMOD.BAS:1640-1646); RemoveOne checks every alignment
- [x] The encounter sound on arriving in a room with monsters has no source found (the implementer of stored rooms could not find one) - confirm or remove
- [x] After a chest is opened, the empty combat panel stays up until the next change of area
- [x] The chest teleporter trap settles the new area at the next combat tick, not at once (approximation)
- [x] Stairs up out of the dungeon mid-fight closes without a leave round - trace the original exit path
- [x] Dungeon side of the city batch: Companion Info in the dungeon should call Librarian.FileCompanion with the "X,Y,level" place (DungeonWindow ~834); the dungeon SheetSpellBox typed-name Info (fn1725); ItemUse.ApplySpecial callers (DungeonWindow ~2216, ~2252) should pass the gear lookup so GearCheck runs before Kind 25 aging
- [x] Turning logs _nav.Describe() (the square label) to Messages (DungeonWindow ~517-528); with the repeat guard gone it repeats on stairs/chutes - check the original
- [x] cmdCast_Click does not reset M5766 (FRMCHAR3.BAS:180), so a spell cast after an item order keeps draining the item; the port drops the item order
- [x] The item-buffer Combat Setting (sub0BDE calling sub15CD, MULTIMOD.BAS:174-175) is not modelled (CombatOrder.cs)
- [x] After a won fight with no chest, the portrait, state icon and captions stay up until you move; the original blanks them (MONSMOD.BAS:780-781)
- [x] GuildData.CanUse checks only the primary guild; fn11B6 accepts any joined guild for items not tied to one guild
- [x] The General Store's Search box runs close to its frame edge
- [x] txtSpell_DragDrop (FRMCHAR3.BAS:845-849): a spell dragged onto the Spells box sets its text - not wired
- [x] The mouse wheel may still scroll the Store Buy list while its scrollbar is disabled during Select Alignment
- [x] The dungeon Objects & Spells list shows item rows of the port's own; lblName holds only the character's spells (sub2424, FRMCHAR3.BAS:681-692) - where do item spells live in the original?
- [x] ClearsItemSlot tells CheckCast refusals apart by their wording; return a refusal kind from CheckCast instead
- [x] sub0BDE spell-setting branch still skips mdrcharCheckCast and sub102F (MULTIMOD.BAS:178-182)
- [x] CityWindow reads a character's area from X,Y in places; use Character.Area (M59BB) there too

## Final audit 2026-09-26: untraced text, rules and dev UI
- [x] City row of text buttons (Control Panel / Automap / Library / Hall of Records / Help Lesson, CityWindow.axaml ~170-176) - cityw1.png has only the 8 picture buttons
- [x] Toolbar tooltips (Toolbar.cs ~75-81) and AutomapWindow "Deeper"/"Up" tooltips - VB3 has none
- [x] "Cast (C)" caption (DungeonWindow.axaml ~271) - the form caption is "&Cast"
- [x] Spell list rows "Name  (n sp)" - sub2424 (FRMCHAR3.BAS:681-712): Name / Guild / Points ("***" > 999) / Level columns, bold when castable, "No Spells Yet", cmbClass class selector (FRMCHAR3.BAS:55-70); and the port hides combat spells outside combat instead of un-bolding
- [x] "You wade through cold water." / "You struggle through quicksand." (DungeonWindow ~2548, ~2552) - no source
- [x] "Under: " prefix on SheetStates and "Protected {n}" (DungeonWindow ~645, Character.cs ~432) - the original shows bare gv07E2$ captions (FRMCHAR1.BAS:174-182, MODULE1.BAS:442-448)
- [x] "Something went wrong there..." on the Wait button (DungeonWindow ~5095) - log only
- [x] Window titles "Mordor — Town" / "Mordor — Dungeon" - the frame caption is "Mordor 1.1" (FRMMAIN.BAS:149-151)
- [x] The Dungeon child is titled plain "Dungeon" when alone (MULTIMOD.BAS:686-690)
- [x] Help search box and fallback text - document as the WinHelp Search stand-in
- [x] Level fee: port fn12B2 (MAINMOD2.BAS:2140-2155) with the loaded GuildDef.GoldFactor instead of the hard-coded GuildGoldFactor table (Rules.cs ~557-570)
- [x] GuildData.Defaults and Spells.GuildSchools fallbacks used silently when MDATA1 fails - remove and fail loudly
- [x] MdrLoader invented clamps (LateHits/AvgHits Max(1), ExpFactor default 8) and the dead "Villian" rename
- [x] Dead code cleanup: Character.RollStats, Combat.Describe, StoreStock.IsStarter, DungeonState.TimeUntilRespawn, GuildDef.IsCaster
- [x] "Run Browser.bat" launches the normal game - rename or fix
- [x] RaceData.cs ~63-71 still keeps fallback defaults and LoadedFromGameData (as GuildData did); GuildDef.FightingMod 0.7f fallback and RaceMask = 0x1FF default are unsourced; GuildData.ByName falls back to All[0]
- [x] RaceData.cs ~63-71 still keeps fallback defaults and LoadedFromGameData; GuildDef.FightingMod 0.7f fallback and RaceMask = 0x1FF default are unsourced; GuildData.ByName falls back to All[0]
- [x] "No Spells Yet" row: sub2424 sets elaSpells.P?C00F = False when empty (FRMCHAR3.BAS:723-727); if that is Enabled the row cannot be clicked, and the port writing "No Spells Yet" into the box on click would be invented - resolve P?C00F
- [x] DungeonNav.SetPosition moves you to the nearest open square when the target is rock (no source), so Teleport/Displacement into rock is survived and Passwall bodies land off the rock (MAINMOD2.BAS:5860-5896); FIDELITY says the port kills the party
- [x] MdrLoader race/guild PlausibleName and sane filters (and the RaceSize.Normal fallback) drop records fn03D0 keeps (MODULE1.BAS:678-716)
- [x] StoreStock.IsStarter is dead code with an untraced rule - delete
- [x] Loot.RollAlignment / ItemAlignments.Possible lack a citation (MONSMOD.BAS:3807-3811)
- [x] After a teleporter square the original sets l0286 and re-runs the square routine on the new square (MAZEMOD.BAS:1823, 1893-1895), so a destination that is a chute, pit etc. fires again; the port only settles and describes
- [x] sub16CC "Reload this Party?" question when a party is unloaded on exit to the main menu is not ported
- [x] Party.LoadFor compares X/Y where mdrmulLoadParty compares M59BB (area) and level (MULTIMOD.BAS:201-274)
- [x] sub16CC also writes MORDOR.INI "Character" for Preload Character (MAINMOD2.BAS:5948-5953) - map to the port's own settings file, never MORDOR.INI
- [x] City Alt+X and q keys (fn1521 in the city) not wired
- [x] OS shutdown / non-user close skips "Exit Mordor?" and sub16CC; the original handles it in MDIForm_QueryUnload (FRMMAIN.BAS:176-181)
- [x] Party.SaveParty drops fn179B's sub04FC error-log line (MULTIMOD.BAS:440)
- [x] A room generated on arrival (fn0D4D ends in fn0F88, MONSMOD.BAS:1979): check the port writes it to dungeon.json then, not only on the next area change - a hard kill lost an unopened chest in testing. It did not; it does now, atomically (FIDELITY "A room is on disk as soon as it is made")
- [ ] Store page still needs scrolling and clips its last count column in a small (~800x560) frame
- [ ] "Game Options..." access key still unresolved (see above)
- [ ] Modding: design parked in docs/MODDING.md. When built, restore the Store alignment lock as the default and move the unlocked behaviour (164befa) into the first mod
