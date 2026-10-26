using Mordor.Core;
/// <summary>The guild level various items need, per guild (fn030F).</summary>
public static class UseLevelProbe { public static void Run(string dir) {
  GuildData.LoadFrom(dir);
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR")).Where(i => i.Id != 0).ToList();
  string[] show = { "Bronze Sword", "Iron Sword", "Padded Leather Armor", "Sword of the Winds", "Ring of Power" };
  Console.Write($"{"item",-24} {"scale",5}");
  foreach (var g in GuildData.All.Take(4)) Console.Write($" {g.Name,10}");
  Console.WriteLine($" {"Healer",10}");
  foreach (string name in show)
  {
    var it = items.FirstOrDefault(i => i.Name == name);
    if (it == null) continue;
    Console.Write($"{it.Name,-24} {it.LevelScale,5}");
    foreach (var g in GuildData.All.Take(4)) Console.Write($" {Character.UseLevel(it, g),10}");
    Console.WriteLine($" {Character.UseLevel(it, GuildData.ByName("Healer")),10}");
  }
  Console.WriteLine();
  Console.WriteLine("guild item-use factors: " + string.Join(", ", GuildData.All.Select(g => $"{g.Name} {g.ItemUseFactor:0.#}")));
} }
