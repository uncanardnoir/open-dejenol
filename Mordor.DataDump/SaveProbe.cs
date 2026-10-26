using Mordor.Core;

/// <summary>Round-trip a character through the JSON save (incl. the automap dictionary) and print the result.</summary>
public static class SaveProbe
{
    public static void Run(string dir)
    {
        Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        var c = Character.Create("RoundTrip", RaceData.ByName("Elf"), Alignment.Good, Sex.Female, new[] { 6, 12, 11, 5, 7, 9 });
        c.Explored[1] = new List<int> { 10, 11, 12, 40, 41 };
        c.Explored[3] = new List<int> { 899 };
        c.PutInPack(new InventorySlot { ItemId = 33, Equipped = true });
        c.JoinGuild(GuildData.ByName("Nomad"));
        c.Guild.Experience = 500; c.Kills = 7;

        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor_roundtrip.json");
        c.Save(path);
        var back = Character.Load(path);
        Console.WriteLine($"saved {new System.IO.FileInfo(path).Length} bytes");
        Console.WriteLine($"name={back.Name} race={back.Race} align={back.Alignment} sex={back.Sex} stats={string.Join(",", back.Stats)}");
        Console.WriteLine($"explored levels={string.Join(",", back.Explored.Keys)}  L1={string.Join(",", back.Explored[1])}  L3={string.Join(",", back.Explored[3])}");
        Console.WriteLine($"inventory={back.Inventory.Count} equipped={back.Inventory[0].Equipped}  guild={back.Guild.Guild} exp={back.Guild.Experience} kills={back.Kills}");
        Console.WriteLine($"canLevelUp={back.CanLevelUp()} (need {back.ExpForNextLevel()})");
        bool ok = back.Name == c.Name && back.Explored[1].SequenceEqual(c.Explored[1]) && back.Explored[3].SequenceEqual(c.Explored[3]) && back.Stats.SequenceEqual(c.Stats);
        Console.WriteLine(ok ? "ROUNDTRIP OK" : "ROUNDTRIP MISMATCH");
    }
}
