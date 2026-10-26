using Mordor.Core;
using System.Text;

/// <summary>
/// <c>summary</c>: the data files parsed, as a first look at a game folder - a few spells, items and
/// monsters, every floor's size, level 1 drawn as an ASCII map, and the library file if there is one.
/// Exit 1 when a file does not parse.
/// </summary>
public static class SummaryDump
{
    public static int Run(string dir)
    {
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
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
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
}
