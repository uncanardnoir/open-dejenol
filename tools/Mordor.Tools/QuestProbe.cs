using Mordor.Core;
/// <summary>How often a quest is handed out, and what it asks for.</summary>
public static class QuestProbe { public static void Run(string dir) {
  GuildData.LoadFrom(dir);
  var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
  var rng = new Random(505);
  Console.WriteLine("guild quest chance: " + string.Join(", ", GuildData.All.Select(g => $"{g.Name} {g.QuestPercent}%")));
  Console.WriteLine();
  Console.WriteLine($"{"guild",-10} {"level",5} {"quests per 1000 levels",23} {"kill",6} {"fetch",6}");
  foreach (string name in new[] { "Nomad", "Warrior", "Mage" })
    foreach (int lvl in new[] { 3, 8, 20, 60 })
    {
      var g = GuildData.ByName(name);
      int got = 0, kill = 0, fetch = 0;
      for (int i = 0; i < 1000; i++)
      {
        var (t, tier) = Rules.RollQuest(g, lvl, rng);
        if (t <= 0) continue;
        got++;
        if (t == 1) kill++; else fetch++;
      }
      Console.WriteLine($"{name,-10} {lvl,5} {got,23} {kill,6} {fetch,6}");
    }
  Console.WriteLine();
  for (int i = 0; i < 4; i++)
  {
    var m = Rules.QuestMonster(monsters, 3, rng);
    var it = Rules.QuestItem(items, 6, rng);
    Console.WriteLine($"  e.g. defeat one {m?.Name ?? "-"}   /   bring back one {it?.Name ?? "-"}");
  }
} }
