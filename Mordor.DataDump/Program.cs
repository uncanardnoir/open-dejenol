using Mordor.Core;
using System.Text;
// the pictures and the help, read out of the game folder as the app does at start (Mordor.Core.Extraction.GameArt)
if (args.Length > 1 && args[1] == "extract") { GameArtDump.Run(args[0], args.Length > 2 ? args[2] : null); return; }
if (args.Length > 1 && args[1] == "rooms") { RoomProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "aligns") { AlignProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "picids") { ArtProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "art") { NeResources.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "spawnsim") { SpawnSim.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "areaid") { AreaIdProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "areas") { AreaProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "rulescheck") { RulesProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "abilities") { AbilityProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "gold") { GoldProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "pack") { PackProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "exchangeswap") { ExchangeSwapProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "slotroll") { SlotRollProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "companionpage") { CompanionPageProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "spellinfo") { SpellInfoProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "library") { LibraryProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "password") { PasswordProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "respawn") { RespawnProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "map") { MapProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "party") { PartyProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "trade") { TradeProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "store") { StoreProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "itemlayout") { ItemLayoutProbe.Run(args[0]); return; }
if (args.Length > 2 && args[1] == "price") { PriceProbe.Run(args[0], args[2]); return; }
if (args.Length > 1 && args[1] == "goldfactor") { GoldFactorProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "goldsim") { GoldSim.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "guildad") { GuildADProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "adcheck") { ADCheck.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "traps") { TrapProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "goldbug") { GoldBugProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "ailments") { AilmentProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "drops") { DropProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "dropsim") { DropSim.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "mix") { MixProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "uselevel") { UseLevelProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "quests") { QuestProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "play") { PlaySim.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "maze") { MazeProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "lairs") { LairProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "rarity") { RarityProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "traptable") { TrapTableProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "shadow") { ShadowProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "hands") { HandsProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "racetable") { RaceTableProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "crestspells") { CrestSpellProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "spellzero") { SpellZeroProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "recordzero") { RecordZeroProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "locks") { LockProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "reach") { ReachProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "classspells") { ClassSpellProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "orphans") { OrphanProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "commands") { CommandProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "acid") { AcidProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "drain") { DrainProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "stateleak") { StateLeakProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "fate") { FateProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "uncurse") { UncurseProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "deepest") { DeepestProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "classdump") { ClassDump.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "handover") { HandoverProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "citations") { Citations.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "helpquotes") { HelpQuotes.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "effects") { EffectsProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "use") { UseProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "equip") { EquipProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "mdata1") { Mdata1Probe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "groups") { GroupProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "depth") { DepthProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "wallart") { WallArtProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "statmods") { StatModProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "monsterattack") { MonsterAttackProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "join") { JoinProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "rescue") { RescueProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "trapread") { TrapReadoutProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "chest") { ChestProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "perceive") { PerceiveProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "misid") { MisidProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "seer") { SeerProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "resist") { ResistProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "companions") { CompanionProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "mflags") { MonsterFlagProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "spellcaps") { SpellCapProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "itemspells") { ItemSpellProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "audio") { AudioProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "magic") { MagicProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "itemtypes") { ItemTypeProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "spellstuff") { SpellstuffProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "spells") { SpellProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "resist") { ResistProbe.Run(args[0]); return; }
if (args.Length > 3 && args[1] == "autoload") { Mdata1.LoadFrom(args[0]); RaceData.LoadFrom(args[0]); GuildData.LoadFrom(args[0]); AutoloadProbe.Run(args[2], args[3]); return; }
if (args.Length > 1 && args[1] == "teleports") { TeleportProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "gearcheck") { GearCheckProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "formfonts") { FormFonts.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "selfcheck") { Environment.ExitCode = SelfCheckProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "mods") { Environment.ExitCode = ModProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "castgate") { CastGateProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "spellhelp") { SpellHelpProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "spellgate") { SpellGateProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "save") { SaveProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "adinterval") { AdIntervalProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "x87") { X87Probe.Run(args[0]); return; }

if (args.Length > 1 && args[1] == "exchange") { ExchangeProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "guildlog") { GuildLogProbe.Run(args[0], args.Length > 2 ? args[2] : null); return; }
if (args.Length > 1 && args[1] == "lost") { LostProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "guildmaster") { GuildmasterProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "raise") { RaiseProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "rating") { RatingProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "hall") { HallProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "asmodeus") { AsmodeusProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "swings") { SwingProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "rules") { RaceGuildProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "gamedata") { GameDataProbe.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "pics") { Pics.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "combat") { Sim.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "orient") { Orient.Run(args[0]); return; }
if (args.Length > 1 && args[1] == "bits") { Bits.Run(args[0]); return; }

string dir = args.Length > 0 ? args[0] : GameFolder.Resolve().Found?.Dir ?? Path.Combine("..", "game_files");
string P(string f) => Path.Combine(dir, f);
Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"Reading Mordor data from: {dir}\n");

try
{
    var spells = MdrLoader.LoadSpells(P("MDATA2.MDR"));
    Console.WriteLine($"SPELLS: {spells.Count}");
    foreach (var s in spells.Take(8)) Console.WriteLine($"  [{s.Id,3}] L{s.Level} {s.Name}");

    var items = MdrLoader.LoadItems(P("MDATA3.MDR"));
    Console.WriteLine($"\nITEMS: {items.Count}");
    foreach (var it in items.Take(12))
        Console.WriteLine($"  [{it.Id,4}] {it.Name,-28} att{it.Att,3} def{it.Def,3} price{it.Price,8} floor{it.Floor,3}");

    var monsters = MdrLoader.LoadMonsters(P("MDATA5.MDR"));
    Console.WriteLine($"\nMONSTERS: {monsters.Count}");
    foreach (var m in monsters.Take(12))
        Console.WriteLine($"  [{m.Id,4}] {m.Name,-26} att{m.Att,3} def{m.Def,3} hits{m.Hits,5} lvl{m.LevelFound,3} {m.Kind}");
    Console.WriteLine("  ...");
    foreach (var m in monsters.TakeLast(3))
        Console.WriteLine($"  [{m.Id,4}] {m.Name,-26} att{m.Att,3} def{m.Def,3} hits{m.Hits,5} lvl{m.LevelFound,3} {m.Kind}");

    var dungeon = MdrLoader.LoadDungeon(P("MDATA11.MDR"));
    Console.WriteLine($"\nDUNGEON: {dungeon.Count} levels");
    foreach (var fl in dungeon)
        Console.WriteLine($"  Level {fl.LevelNum,2}: {fl.Width}x{fl.Height}, {fl.Teleporters.Count} teleporters, {fl.Chutes.Count} chutes");

    // Render level 1 as an ASCII map
    var floor = dungeon[0];
    Console.WriteLine($"\nLEVEL {floor.LevelNum} MAP ({floor.Width}x{floor.Height}):");
    Console.WriteLine(RenderMap(floor));

    // MDATA13 is not shipped with the game: MAINMOD.BAS:729 builds it at startup under
    // "Creating Library File..." from the tables just loaded, and deletes any existing copy
    // first. So it is only there to read if a real installation has already run.
    string libPath = P("MDATA13.MDR");
    if (File.Exists(libPath))
    {
        var lib = MdrLoader.LoadLibrary(libPath);
        Console.WriteLine($"LIBRARY: {lib.Monsters.Count} monster entries, {lib.Items.Count} item entries " +
                          $"(found {lib.MonstersFound} monsters, {lib.ItemsFound} items)");
    }
    else
        Console.WriteLine("LIBRARY: MDATA13.MDR is absent, which is normal - the game generates it at "
                        + "startup from the monster and item tables (MAINMOD.BAS:729).");

    Console.WriteLine("\nOK - all data files parsed successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"\nERROR: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

static string RenderMap(Floor f)
{
    // Each cell = 2 chars wide. Draw north/east walls; '+' corners.
    var sb = new StringBuilder();
    for (int y = 0; y < f.Height; y++)
    {
        // north walls row
        for (int x = 0; x < f.Width; x++)
            sb.Append('+').Append(f.Has(x, y, Floor.WALL_NORTH) ? "--" : "  ");
        sb.Append("+\n");
        // cell + east walls row
        for (int x = 0; x < f.Width; x++)
        {
            sb.Append('|'); // west edge approximation via each cell's own drawing
            sb.Append(CellChar(f, x, y));
            sb.Append(f.Has(x, y, Floor.WALL_EAST) ? "|" : " ");
            if (x == 0) { } // west wall handled by previous '|'
        }
        sb.Append('\n');
    }
    // bottom border
    for (int x = 0; x < f.Width; x++) sb.Append("+--");
    sb.Append("+\n");
    return sb.ToString();
}

static string CellChar(Floor f, int x, int y)
{
    if (f.Has(x, y, Floor.ROCK)) return "██";
    if (f.Has(x, y, Floor.STAIRS_UP)) return "<>";
    if (f.Has(x, y, Floor.STAIRS_DOWN)) return "><";
    if (f.Has(x, y, Floor.CHUTE)) return "vv";
    if (f.Has(x, y, Floor.TELEPORTER)) return "TT";
    if (f.Has(x, y, Floor.PIT)) return "()";
    if (f.Has(x, y, Floor.WATER)) return "~~";
    if (f.Has(x, y, Floor.ANTIMAGIC)) return "AM";
    if (f.Has(x, y, Floor.FOG)) return "::";
    return "  ";
}
