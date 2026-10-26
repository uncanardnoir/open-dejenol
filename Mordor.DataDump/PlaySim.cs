using Mordor.Core;

/// <summary>
/// Plays whole fights headlessly through the real CombatEngine, so every rule ported from the
/// source is exercised together. The character is equipped with the best gear their guild level
/// allows, which is what a real player would be doing.
/// </summary>
public static class PlaySim { public static void Run(string dir) {
  Mdata1.LoadFrom(dir); RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
  var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
  var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
  var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
  var rng = new Random(20260918);

  Console.WriteLine($"{"lvl",3} {"glvl",5} {"won%",5} {"died%",6} {"foes",5} {"hits",6} {"A/D",9}  commonest cause of death");
  // the monster table says what each floor expects: floor 1 wants guild level 2, floor 6 wants 55
  foreach (var (lvl, glvl) in new[] { (1, 2), (2, 10), (3, 25), (5, 45), (8, 85), (12, 125), (15, 200) })
  {
    int won = 0, died = 0, foes = 0;
    var causes = new Dictionary<string, int>();
    int hits = 0, att = 0, def = 0;
    const int N = 400;
    for (int i = 0; i < N; i++)
    {
      var ch = Character.Create("Sim", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 16, 10, 10, 16, 10, 16 });
      for (int l = 1; l < glvl; l++) ch.LevelUp(rng);
      // wear the best weapon and armour the guild level actually permits
      foreach (int kind in new[] { 0, 1, 2, 8, 9, 10, 11, 12 })
      {
        var best = items.Where(it => it.Id != 0 && it.Type == kind && it.Floor <= lvl + 1
                                  && ch.EquipProblem(it) == null)
                        .OrderByDescending(it => it.Att + it.Def).FirstOrDefault();
        if (best != null) ch.PutInPack(new InventorySlot { ItemId = best.Id, Equipped = true, Known = IdLevel.All });
      }
      ch.CurHits = ch.MaxHits; ch.DungeonLevel = lvl;

      var group = EncounterGenerator.Generate(monsters, lvl, rng);
      if (group.Count == 0) continue;
      foes += group.Count;
      var eng = new CombatEngine(ch, group, id => byId.GetValueOrDefault(id), rng);
      eng.UseMonsterTable(monsters);
      hits += ch.MaxHits;
      att += eng.AttackOf(ch); def += eng.DefenseOf(ch);

      for (int round = 0; round < 60 && !eng.IsOver; round++)
      {
        eng.PlayerAttacks(ch);
        if (eng.IsOver) break;
        foreach (var _ in eng.MonstersAttack()) { }
      }
      if (eng.Won) won++;
      else
      {
        died++;
        string why = ch.Stoned ? "turned to stone" : eng.Log.LastOrDefault(l => l.Contains("dies") || l.Contains("die")) ?? "worn down";
        if (why.Contains("STONE")) why = "turned to stone";
        else if (why.Contains("breath") || why.Contains("burns")) why = "breath";
        else if (why.Contains("electro")) why = "electrocuted";
        else why = "worn down in melee";
        causes[why] = causes.GetValueOrDefault(why) + 1;
      }
    }
    string top = causes.Count == 0 ? "-" : string.Join(", ", causes.OrderByDescending(c => c.Value).Take(2).Select(c => $"{c.Key} {100 * c.Value / Math.Max(1, died)}%"));
    Console.WriteLine($"{lvl,3} {glvl,5} {100.0 * won / N,4:0}% {100.0 * died / N,5:0}% {(double)foes / N,5:0.0} {hits / N,6} {att / N,4}/{def / N,-4}  {top}");
  }
} }
