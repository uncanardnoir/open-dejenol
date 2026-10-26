using Mordor.Core;

// The developer's tools: data dumps, rule probes and simulations that print what the port does with
// the player's game files. They read the game folder and never assert - the checks that do are in
// Mordor.Tests. Every tool is one line of the table below; docs/TOOLS.md lists them too.
//
//   Mordor.Tools help                        the table
//   Mordor.Tools <gamedir> <tool> [args]     run one
//   Mordor.Tools <gamedir>                   the summary dump
//   Mordor.Tools --sweep-list                the tools tools/check.sh runs, for a crash
//
// Some tools print analyses that live in Mordor.Tests because selfcheck asserts on them
// (castgate, commands, fate, formfonts, import, spellhelp, stateleak); this project references it.

var tools = new List<Tool>
{
    // ---- the game folder and its files ----
    new("gamefolder", "files", "the folder as the app's start sees it (GameFiles.Check): exit 0 for a Mordor to play, 2 for none",
        (dir, _) => { var f = GameFiles.Check(dir); Console.WriteLine($"{f.Verdict}: {f.Reason}" + (f.Recognised != null ? $" [{f.Recognised}]" : "") + $" ({f.Dir})"); return f.IsPlayable ? 0 : 2; }),
    new("summary", "files", "the data files parsed: a few spells, items and monsters, every floor's size, level 1 as an ASCII map",
        (dir, _) => SummaryDump.Run(dir)),
    new("extract", "files", "[outdir] the pictures and the help read out of the game folder as the app does (GameArt), written to ../extracted-art/cache",
        (dir, a) => Do(() => GameArtDump.Run(dir, Arg(a, 0))), Sweep: false),
    new("import", "files", "[datadir [charstat-folder]] an original DATA folder read through the importer (or MORDOR_ORIGINAL_DATA): every character and note",
        (dir, a) => Do(() => ImportProbe.Run(dir, Arg(a, 0), Arg(a, 1)))),
    new("mdata1", "files", "MDATA1 walked the way the game walks it (fn03D0): the version, the counts, every race, guild and class record",
        (dir, _) => Do(() => Mdata1Probe.Run(dir))),
    new("formfonts", "files", "every control's design-time font and Tag in the forms MORDOR.EXE carries",
        (dir, _) => Do(() => FormFonts.Run(dir))),
    new("audio", "files", "every WAV and MID in the game folder, opened through the Windows multimedia API without playing anything",
        (dir, _) => Do(() => AudioProbe.Run(dir))),
    new("autoload", "files", "<savesdir> <leader> Party.LoadFor against a saves folder: what mdrmulAutoLoad says, refusals and all",
        (dir, a) =>
        {
            if (a.Length < 2) { Console.WriteLine("usage: Mordor.Tools <gamedir> autoload <savesdir> <leader>"); return 2; }
            Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
            AutoloadProbe.Run(a[0], a[1]);
            return 0;
        }, Sweep: false),
    new("save", "files", "a character round-tripped through the JSON save (in the temp folder), automap and all",
        (dir, _) => Do(() => SaveProbe.Run(dir))),

    // ---- the data against the community's tables ----
    new("lairs", "data", "every lair (area LairId) against the community's Laired_Monster_List",
        (dir, _) => Do(() => LairProbe.Run(dir))),
    new("rarity", "data", "the monsters' Rarity field against the community's Rare_Monsters page",
        (dir, _) => Do(() => RarityProbe.Run(dir))),
    new("racetable", "data", "the race table as loaded (stats, alignments, size, resistances, guilds), laid out like the Races page",
        (dir, _) => Do(() => RaceTableProbe.Run(dir))),
    new("traptable", "data", "MDATA5's TrapFlags against the community's Monster_Trap_Table",
        (dir, _) => Do(() => TrapTableProbe.Run(dir))),
    new("adcheck", "data", "Rules.GuildAD against every value in the community's A-D_For_all_Guild_Levels tables",
        (dir, _) => Do(() => ADCheck.Run(dir))),
    new("rulescheck", "data", "the ported formulas (A/D, experience, gold to level, hit chance) beside the numbers dejenol.com quotes",
        (dir, _) => Do(() => RulesProbe.Run(dir))),
    new("drops", "data", "the two fields item drops hang on (ItemChance, ItemDropLevel) and the deep-dropping monsters",
        (dir, _) => Do(() => DropProbe.Run(dir))),
    new("classdump", "data", "the wear classes out of MDATA1 and the maximum each allows",
        (dir, _) => Do(() => ClassDump.Run(dir))),
    new("mflags", "data", "which monsters carry each special attack, special property and spell flag bit",
        (dir, _) => Do(() => MonsterFlagProbe.Run(dir))),
    new("spells", "data", "the spell table by school, every field of every record",
        (dir, _) => Do(() => SpellProbe.Run(dir))),
    new("areas", "data", "the spawn areas of floors 1-3: kinds allowed, lair monster, what each floor can spawn",
        (dir, _) => Do(() => AreaProbe.Run(dir))),
    new("teleports", "data", "every teleporter in the dungeon and where it puts you, rock and off-map destinations marked",
        (dir, _) => Do(() => TeleportProbe.Run(dir))),
    new("shadow", "data", "which monsters can be drawn at all, and from which floor (the encounter window, Stud squares)",
        (dir, _) => Do(() => ShadowProbe.Run(dir))),
    new("deepest", "data", "the fifteenth floor against the two shelves that stock from the deepest level reached",
        (dir, _) => Do(() => DeepestProbe.Run(dir))),
    new("store", "data", "the store's fixed-price goods (Floor 0) by the original's own test",
        (dir, _) => Do(() => StoreProbe.Run(dir))),
    new("price", "data", "[name] modItemCost's price at stock 1-3 for the items whose name contains <name> (every item without one)",
        (dir, a) => Do(() => PriceProbe.Run(dir, Arg(a, 0) ?? ""))),
    new("orphans", "data", "the two spells no guild reaches (Fate, Life Domination): whether anything else casts them",
        (dir, _) => Do(() => OrphanProbe.Run(dir))),
    new("reach", "data", "every spell effect the data uses against what the port's dispatcher handles",
        (dir, _) => Do(() => ReachProbe.Run(dir))),
    new("castgate", "data", "the COMBAT / NON-COMBAT label computed for every spell (fn149E) against the help's own label",
        (dir, _) => Do(() => CastGateProbe.Run(dir))),
    new("spellhelp", "data", "MDATA2 and MDATA1 against the header the help prints above every spell",
        (dir, _) => Do(() => SpellHelpProbe.Run(dir))),
    new("helpquotes", "data", "every passage the docs quote from the help, checked against the help itself",
        (dir, _) => Do(() => HelpQuotes.Run(dir))),
    new("commands", "data", "the help's Commands page against the keys the port's handlers bind",
        (dir, _) => Do(() => CommandProbe.Run(dir))),

    // ---- the rules, exercised ----
    new("rooms", "rules", "how many rooms on floors 1-3 hold monsters, the group sizes, the respawn delay",
        (dir, _) => Do(() => RoomProbe.Run(dir))),
    new("respawn", "rules", "when a cleared room fills again (sub0A91) and the oddities of the original's midnight timer",
        (dir, _) => Do(() => RespawnProbe.Run(dir))),
    new("map", "rules", "what the automap is told against what is really there (fn2EB5, medPlotMaze)",
        (dir, _) => Do(() => MapProbe.Run(dir))),
    new("maze", "rules", "the view selector walked over every open square of level 1 in all four facings",
        (dir, _) => Do(() => MazeProbe.Run(dir))),
    new("lost", "rules", "being lost on a rotator square: the chance, and the steps to work out where you are",
        (dir, _) => Do(() => LostProbe.Run(dir))),
    new("perceive", "rules", "noticing the square you are on (sub087C): how often, how right, how sure",
        (dir, _) => Do(() => PerceiveProbe.Run(dir))),
    new("join", "rules", "hostility and the offer to join: how often, and what accepting does",
        (dir, _) => Do(() => JoinProbe.Run(dir))),
    new("misid", "rules", "mis-identification of a creature: how often, as what, for how long",
        (dir, _) => Do(() => MisidProbe.Run(dir))),
    new("monsterattack", "rules", "every monster's whole turn (sub0E85) run against a durable character: each special attack reached",
        (dir, _) => Do(() => MonsterAttackProbe.Run(dir))),
    new("acid", "rules", "who an acid attack lands on, and the two companion-slot faults the original's draw has",
        (dir, _) => Do(() => AcidProbe.Run(dir))),
    new("drain", "rules", "which stats a drain attack can take (never Strength)",
        (dir, _) => Do(() => DrainProbe.Run(dir))),
    new("classspells", "rules", "the spells that harm one class of creature, cast at the right class and the wrong one",
        (dir, _) => Do(() => ClassSpellProbe.Run(dir))),
    new("fate", "rules", "what Fate does, measured down both routes that reach it",
        (dir, _) => Do(() => FateProbe.Run(dir))),
    new("stateleak", "rules", "Levitate, Invisible, See Invisible and Protect cast one at a time: which arrive alone",
        (dir, _) => Do(() => StateLeakProbe.Run(dir))),
    new("ailments", "rules", "how long poison and disease last and what they cost, at various CON",
        (dir, _) => Do(() => AilmentProbe.Run(dir))),
    new("traps", "rules", "how often chests appear and which traps they carry, per floor",
        (dir, _) => Do(() => TrapProbe.Run(dir))),
    new("trapread", "rules", "the chest panel (monShowChest): how often the named trap is the real one, the disarm gauge",
        (dir, _) => Do(() => TrapReadoutProbe.Run(dir))),
    new("chest", "rules", "what a chest gives up (monOpenChest): how often, how many items, which ones the pick reaches",
        (dir, _) => Do(() => ChestProbe.Run(dir))),
    new("locks", "rules", "a locked chest opened end to end: by a known spell, and by an item with charges",
        (dir, _) => Do(() => LockProbe.Run(dir))),
    new("slotroll", "rules", "what destroying and stealing attacks pick: a slot, not one of the things carried",
        (dir, _) => Do(() => SlotRollProbe.Run(dir))),
    new("exchangeswap", "rules", "the Exchange window's swap (pnlItem_DragDrop): pack slots by index, slot 0 refused, the item re-identified",
        (dir, _) => Do(() => ExchangeSwapProbe.Run(dir))),
    new("exchange", "rules", "the Exchange screen's rules: refusals, gold above and below ground, identification on hand-over",
        (dir, _) => Do(() => ExchangeProbe.Run(dir))),
    new("handover", "rules", "an item handed to another character re-reads how well they know it (FRMEXCHA.BAS:199-201)",
        (dir, _) => Do(() => HandoverProbe.Run(dir))),
    new("uncurse", "rules", "the Seer's uncurse questions, and which a cursed item answers by how it came into the pack",
        (dir, _) => Do(() => UncurseProbe.Run(dir))),
    new("pack", "rules", "the forty fixed pack slots: slot numbers that stay put when something leaves",
        (dir, _) => Do(() => PackProbe.Run(dir))),
    new("party", "rules", "the party roster and the spell buffers: leaving, swapping, pruning",
        (dir, _) => Do(() => PartyProbe.Run(dir))),
    new("equip", "rules", "whether an item goes on and what it is worth (fn11B6, sub0F74): the class and worth tables, every refusal",
        (dir, _) => Do(() => EquipProbe.Run(dir))),
    new("use", "rules", "the Use and Drop commands: each special type, the refusals, an item of another alignment",
        (dir, _) => Do(() => UseProbe.Run(dir))),
    new("gearcheck", "rules", "the guild-join gear check watched: an item one guild may use and the next may not",
        (dir, _) => Do(() => GearCheckProbe.Run(dir))),
    new("trade", "rules", "the store's trading: what buying and selling do to the shelf, Combine, the other adventurers' restock",
        (dir, _) => Do(() => TradeProbe.Run(dir))),
    new("seer", "rules", "the Seer: name matching, his fee, his refusals, what he finds",
        (dir, _) => Do(() => SeerProbe.Run(dir))),
    new("rescue", "rules", "getting a body out of the dungeon: the wait at each depth, the morgue's fee, how often rescuers return",
        (dir, _) => Do(() => RescueProbe.Run(dir))),
    new("raise", "rules", "who can be brought back and by what (fn142D), and the chance",
        (dir, _) => Do(() => RaiseProbe.Run(dir))),
    new("rating", "rules", "the Guild tab's ability ratings against fn123F",
        (dir, _) => Do(() => RatingProbe.Run(dir))),
    new("swings", "rules", "Multiple Swings: the guild level each guild grants another, and what a round becomes",
        (dir, _) => Do(() => SwingProbe.Run(dir))),
    new("adinterval", "rules", "when a guild awards Att/Def above its cap, from the original's own code",
        (dir, _) => Do(() => AdIntervalProbe.Run(dir))),
    new("spellgate", "rules", "why a caster does or does not know a spell: the guild spell table's gates, then Spellbook.Known",
        (dir, _) => Do(() => SpellGateProbe.Run(dir))),
    new("quests", "rules", "how often a quest is handed out per guild and level, and what it asks for",
        (dir, _) => Do(() => QuestProbe.Run(dir))),
    new("guildlog", "rules", "[logdir] the guild log's rules (dating, halls, Clean); with a folder, what that log holds",
        (dir, a) => Do(() => GuildLogProbe.Run(dir, Arg(a, 0)))),
    new("guildmaster", "rules", "the guildmasters: who holds each guild, when it changes hands, the crests, the demotion",
        (dir, _) => Do(() => GuildmasterProbe.Run(dir))),
    new("hall", "rules", "the Hall of Records: how a record changes hands, what is announced, the computed measures",
        (dir, _) => Do(() => HallProbe.Run(dir))),
    new("asmodeus", "rules", "the endgame: where Asmodeus is found, what his chest yields, the ending text",
        (dir, _) => Do(() => AsmodeusProbe.Run(dir))),
    new("effects", "rules", "the effect and resistance model: what each race resists, a worn item's worth, each chest trap",
        (dir, _) => Do(() => EffectsProbe.Run(dir))),
    new("magic", "rules", "the spell system and the loot rolls exercised without the UI",
        (dir, _) => Do(() => MagicProbe.Run(dir))),
    new("x87", "rules", "where the port's Doubles and the x87's 80-bit intermediates would give a different whole number",
        (dir, _) => Do(() => X87Probe.Run(dir))),

    // ---- the windows' text ----
    new("library", "text", "the Library's prose (fn1BBE) at each of the four grades",
        (dir, _) => Do(() => LibraryProbe.Run(dir))),
    new("spellinfo", "text", "the Misc tab's spell page for spells of different classes",
        (dir, _) => Do(() => SpellInfoProbe.Run(dir))),
    new("companionpage", "text", "the Misc tab's companion page at each ID grade, and the kick-out roll",
        (dir, _) => Do(() => CompanionPageProbe.Run(dir))),
    new("password", "text", "character passwords: the stored space, the skipped prompt, the case-sensitive compare",
        (dir, _) => Do(() => PasswordProbe.Run(dir))),

    // ---- simulations ----
    new("spawnsim", "sims", "20,000 encounters over every walkable square of floors 1-3: which monsters lead them",
        (dir, _) => Do(() => SpawnSim.Run(dir))),
    new("goldsim", "sims", "what the gold formula pays per floor, beside the cost of making a level",
        (dir, _) => Do(() => GoldSim.Run(dir))),
    new("dropsim", "sims", "what item generation yields per floor: chests, items, the commonest finds",
        (dir, _) => Do(() => DropSim.Run(dir))),
    new("combat", "sims", "random encounters fought per floor through the CombatEngine: wins, deaths, flights, experience, gold, rounds",
        (dir, _) => Do(() => Sim.Run(dir))),
    new("play", "sims", "whole fights by a character in the best gear their level allows, floor by floor (long)",
        (dir, _) => Do(() => PlaySim.Run(dir)), Sweep: false),
};
var byName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);

if (args.Length == 1 && args[0] == "--sweep-list")
{
    foreach (var t in tools.Where(t => t.Sweep)) Console.WriteLine(t.Name);
    return 0;
}
if (args.Length == 0 || args[0] is "help" or "-h" or "--help" || (args.Length > 1 && args[1] is "help" or "-h" or "--help"))
{
    Console.WriteLine("usage: Mordor.Tools <gamedir> <tool> [args]     (Mordor.Tools <gamedir> alone: the summary)");
    string? group = null;
    foreach (var t in tools)
    {
        if (t.Group != group) { group = t.Group; Console.WriteLine($"\n{Groups.Title(group)}"); }
        Console.WriteLine($"  {t.Name,-14} {t.Description}{(t.Sweep ? "" : "  [not in check.sh's sweep]")}");
    }
    return args.Length == 0 ? 2 : 0;
}
if (args.Length == 1) return SummaryDump.Run(args[0]);
if (!byName.TryGetValue(args[1], out var tool))
{
    Console.WriteLine($"no tool called \"{args[1]}\" (Mordor.Tools help lists them)");
    return 2;
}
return tool.Run(args[0], args.Skip(2).ToArray());

static int Do(Action run) { run(); return 0; }
static string? Arg(string[] a, int i) => a.Length > i ? a[i] : null;

/// <summary>One tool: its name on the command line, its group in the help, one line saying what it prints.</summary>
/// <param name="Sweep">tools/check.sh runs it for a crash; false for the ones that write files, need arguments or do not end soon.</param>
record Tool(string Name, string Group, string Description, Func<string, string[], int> Run, bool Sweep = true);

static class Groups
{
    public static string Title(string group) => group switch
    {
        "files" => "the game folder and its files",
        "data" => "the data, against the community's tables and the help",
        "rules" => "the rules, exercised",
        "text" => "the windows' text",
        "sims" => "simulations",
        _ => group,
    };
}
