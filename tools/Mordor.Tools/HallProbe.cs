using Mordor.Core;

/// <summary>
/// Checks the Hall of Records (FRMHALL.BAS, MODULE11.BAS sub1043 and fn1BF0): how a record
/// changes hands, which changes are announced, and what the four computed measures come to.
/// </summary>
public static class HallProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Lookup(short id) => byId.TryGetValue(id, out var x) ? x : null;

        string saves = Path.Combine(Path.GetTempPath(), "mordor-hall-probe");
        if (Directory.Exists(saves)) Directory.Delete(saves, recursive: true);
        Directory.CreateDirectory(saves);

        Character Make(string name, string guild, int level, int[]? stats = null)
        {
            var c = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     stats ?? new[] { 12, 12, 12, 12, 12, 12 });
            c.Guilds.Clear();
            c.Guilds.Add(new GuildMembership { Guild = guild, Level = level });
            return c;
        }

        // ---- the deadliest creatures the game knows ----
        Console.WriteLine("the ten most formidable monsters by fn1BF0's reckoning:");
        foreach (var m in monsters.Where(m => m.Id != 0).OrderByDescending(HallRecords.MonsterPower).Take(10))
            Console.WriteLine($"  {HallRecords.MonsterPower(m),6}  {m.Name,-28} att {m.Att,3} hits {m.Hits,5} " +
                              $"lvl {m.GuildLevel,3} STR {m.Stats.ElementAtOrDefault(0),3}");
        var weakest = monsters.Where(m => m.Id != 0).OrderBy(HallRecords.MonsterPower).First();
        Console.WriteLine($"  and the least: {HallRecords.MonsterPower(weakest)} for the {weakest.Name}");

        // ---- how a record changes hands ----
        Console.WriteLine("\nhow the Strongest record changes hands:");
        var hall = new HallRecords();
        void Offer(string who, int str)
        {
            var c = Make(who, "Warrior", 5, new[] { str, 12, 12, 12, 12, 12 });
            string? said = hall.Check(HallRecords.Kind.Strength, c, str);
            var r = hall.For(HallRecords.Kind.Strength);
            Console.WriteLine($"  {who,-8} offers {str,3}: {(said ?? "(nothing announced)"),-62} held by " +
                              $"{(r.Holder == "" ? "nobody" : r.Holder)} at {r.Value}");
        }
        Offer("Brannoc", 14);
        Offer("Sela", 14);      // equal is not enough
        Offer("Sela", 16);
        Offer("Sela", 18);      // improving your own record is silent
        Offer("Kord", 21);

        // An unset creature record holds 0, and monster 0 is the GOBLIE template - so the bar
        // starts at a goblin's worth of menace rather than at nothing. The original does the
        // same: fn1BF0 looks the value up by monster id and finds that template.
        var goblie = monsters.First(m => m.Id == 0);
        Console.WriteLine($"\nan unset creature record resolves to monster 0, the {goblie.Name} template, " +
                          $"worth {HallRecords.MonsterPower(goblie)} - that is the bar a first kill has to clear.");
        Console.WriteLine("the record is kept but never announced (the original reuses one flag for two cases):");
        var hunter = Make("Hunter", "Ninja", 20);
        int PowerOf(int id) => monsters.FirstOrDefault(x => x.Id == id) is Monster d ? HallRecords.MonsterPower(d) : 0;
        foreach (var m in monsters.Where(m => m.Id != 0).OrderBy(HallRecords.MonsterPower).Take(1)
                    .Concat(monsters.Where(m => m.Id != 0 && HallRecords.MonsterPower(m) > HallRecords.MonsterPower(goblie))
                                    .OrderBy(HallRecords.MonsterPower).Take(1))
                    .Concat(monsters.Where(m => m.Id != 0).OrderByDescending(HallRecords.MonsterPower).Take(1)))
        {
            long before = hall.For(HallRecords.Kind.DeadliestCreature).Value.Whole;
            string? said = hall.Check(HallRecords.Kind.DeadliestCreature, hunter, m.Id, PowerOf);
            long after = hall.For(HallRecords.Kind.DeadliestCreature).Value.Whole;
            string held = monsters.FirstOrDefault(x => x.Id == after)?.Name ?? "(none)";
            Console.WriteLine($"  killed a {m.Name,-24} ({HallRecords.MonsterPower(m),5}) " +
                              $"{(after != before ? "takes it" : "not enough"),-11} announced: {said ?? "no"}   record: {held}");
        }

        // ---- the three skill measures ----
        Console.WriteLine("\nthe three skill measures, by guild and level:");
        Console.WriteLine($"  {"guild",-10} {"lvl",4} {"fighting",9} {"magic",6} {"thieving",9}");
        foreach (string g in new[] { "Nomad", "Warrior", "Ninja", "Thief", "Wizard", "Healer" })
            foreach (int lvl in new[] { 1, 20, 80 })
            {
                var c = Make("X", g, lvl, new[] { 18, 18, 18, 18, 18, 18 });
                Console.WriteLine($"  {g,-10} {lvl,4} {HallRecords.FightingSkill(c),9} " +
                                  $"{HallRecords.MagicSkill(c, spells),6} {HallRecords.ThievingSkill(c, Lookup),9}");
            }

        // ---- what a new game already has on the board ----
        Console.WriteLine("\nwhat a brand-new game's Hall already holds (MAINMOD.BAS:662):");
        var seeded = HallRecords.Fresh();
        foreach (HallRecords.Kind k in Enum.GetValues<HallRecords.Kind>())
            Console.WriteLine($"  {HallRecords.Heading(k),-38} {seeded.For(k).Holder,-16} {seeded.For(k).Value,12:N0}");

        // ---- what the Hall shows ----
        Console.WriteLine("\nwhat the Hall reads with nothing set, and once it is:");
        var fresh = new HallRecords();
        Console.WriteLine("  " + fresh.Describe(HallRecords.Kind.Wisdom, v => $"With a {v} Wisdom").Replace("\n", " / "));
        var champ = Make("Champion", "Warrior", 9);
        fresh.Check(HallRecords.Kind.Wisdom, champ, 21);
        Console.WriteLine("  " + fresh.Describe(HallRecords.Kind.Wisdom, v => $"With a {v} Wisdom").Replace("\n", " / "));

        // ---- the guild-log line ----
        var logged = new HallRecords();
        var rich = Make("Croesus", "Nomad", 3);
        logged.CheckAndLog(HallRecords.Kind.Wealthiest, rich, 1_250_000, saves);
        Console.WriteLine("\n  guild log: " + string.Join(" | ", GuildLog.All(saves).Select(e => e.Text)));

        Directory.Delete(saves, recursive: true);
    }
}
