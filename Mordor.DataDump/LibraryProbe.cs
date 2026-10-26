using Mordor.Core;

/// <summary>
/// The Library's prose, <c>fn1BBE</c> (MODULE11.BAS:78-406). The point of it is the four grades:
/// the same creature reads very differently depending on how much the world has worked out.
/// </summary>
public static class LibraryProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR")).Where(m => m.Id != 0).ToList();
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));
        Librarian.SpellNameOf = i => i >= 0 && i < spells.Count ? spells[i].Name.Trim() : "a spell";

        int today = Librarian.Serial(new DateTime(2026, 9, 21));
        var beast = monsters.First(m => m.SpecialAttackFlags != 0 && m.Resistances.Any(r => r > 0));

        Console.WriteLine($"the same creature at each of the four grades - {beast.Name}:\n");
        foreach (int grade in new[] { Librarian.VeryLittle, Librarian.AGoodDealMore,
                                      Librarian.AlmostEverything, Librarian.Everything })
        {
            var e = new LibraryRecord
            {
                Known = (short)grade, NSeen = 47, NDeaths = 3,
                Name = "Crashland", Location = "level 4", LastSeen = today - 2,
            };
            Console.WriteLine($"---- grade {grade} ----");
            Console.WriteLine(Librarian.DescribeMonster(beast, e, today, beast.Name + "s"));
            Console.WriteLine();
        }

        Console.WriteLine("a creature nobody has written up yet:");
        Console.WriteLine(Librarian.DescribeMonster(monsters[0], new LibraryRecord(), today, monsters[0].Name + "s"));

        var relic = items.First(i => i.SpellIndex > 0 && i.Cursed != 0)
                 ?? items.First(i => i.SpellIndex > 0);
        Console.WriteLine($"\n\nan item at each of its two grades - {relic.Name}:\n");
        foreach (int grade in new[] { Librarian.AlmostEverything, Librarian.Everything })
        {
            var e = new LibraryRecord
            {
                Known = (short)grade, NSeen = 12, Name = "Sela",
                Location = "7", LastSeen = today - 1, MonId = monsters[3].Id,
            };
            Console.WriteLine($"---- grade {grade} ----");
            Console.WriteLine(Librarian.DescribeItem(relic, e, today, id =>
                monsters.FirstOrDefault(m => m.Id == id)?.Name ?? ""));
            Console.WriteLine();
        }
    }
}
