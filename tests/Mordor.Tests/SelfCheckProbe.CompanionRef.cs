using System.Text.Json;
using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// Companions and pens keyed by the monster table index, as the original keys them (2026-10-18).
    /// The port had kept only the monster Id, and MDATA5 gives Id 307 to two creatures - the Morgul
    /// and the Shadow Ninja - which the original, keeping the table index (<c>Ref</c>), never mixes
    /// up. Old saves are moved across on load; these checks pin how.
    /// </summary>
    private static void CompanionRefChecks(string dir)
    {
        Console.WriteLine("\ncompanions and pens keyed by table index, Ref (FRMCITYC.BAS:288-291, MAINMOD2.BAS:388-389)");
        string Norm(string s) => s.Replace("\r\n", "\n");
        string cityc = Norm(PortSource("decompiled-named", "FRMCITYC.BAS"));
        string main = Norm(PortSource("decompiled-named", "MAINMOD.BAS"));
        string main2 = Norm(PortSource("decompiled-named", "MAINMOD2.BAS"));
        string mod1 = Norm(PortSource("decompiled-named", "MODULE1.BAS"));

        // ---- the original ----
        Check("MAINMOD2.BAS: fn1163 keeps the table index and the Id beside it",
              main2.Contains("            Chars(pv00D2).Companions(l00DC).Ref = p00D6.Ref\n            Chars(pv00D2).Companions(l00DC).M57D4 = Monsters(p00D6.Ref).Id"), true);
        Check("FRMCITYC.BAS: Buy fills Ref with the row's table index",
              cityc.Contains("    l00B4 = Val(lblSelectedComp.Tag)\n    Chars(Me).Companions(l00B2).Name = \"\"\n    Chars(Me).Companions(l00B2).Ref = l00B4\n    Chars(Me).Companions(l00B2).M57D4 = Monsters(l00B4).Id"), true);
        Check("FRMCITYC.BAS: the pens are gv162E(Ref) - Buy takes from the row's, Sell adds to the companion's",
              cityc.Contains("gv162E(l00B4).Align(m002C%) = gv162E(l00B4).Align(m002C%) - 1")
              && cityc.Contains("gv162E(Chars(Me).Companions(l0126).Ref).Align(Chars(Me).Companions(l0126).Alignment) + 1"), true);
        Check("MAINMOD.BAS: sub054E fills the pens by table index",
              main.Contains("                gv162E(l0126).M5766 = l0126\n                gv162E(l0126).Id = Monsters(l0126).Id"), true);
        Check("MAINMOD2.BAS: the COMPANION SORTER moves a Ref whose creature has another Id to the first index with it",
              main2.Contains("                If  Chars(pv06CE).Companions(l06D0).M57D4 <> Monsters(Chars(pv06CE).Companions(l06D0).Ref).Id Then\n                    sub085B \"COMPANION SORTER: Remapping Companion #\" + LTrim$(Str$(l06D0))\n                    For l06D6 = 0 To gv1BF6 - 1\n                        If  Monsters(l06D6).Id = Chars(pv06CE).Companions(l06D0).M57D4 Then\n                            Chars(pv06CE).Companions(l06D0).Ref = l06D6\n                            Exit For"), true);
        Check("MODULE1.BAS: a store file of another data version is moved by Id, to every index with it",
              mod1.Contains("                                For l06CE = 0 To gv1BF6 - 1\n                                    If  Monsters(l06CE).Id = l06D6(l06C4).Id Then\n                                        gv162E(l06CE) = l06D6(l06C4)\n                                        gv162E(l06CE).M5766 = l06CE"), true);

        // ---- the data ----
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        Check("the loader numbers the table: Ref is the index", monsters.Select((m, i) => m.Ref == i).All(x => x), true);
        var shared = monsters.GroupBy(m => m.Id).Where(g => g.Count() > 1).ToList();
        Check("MDATA5: the one Id two creatures share is 307 - the Morgul at 278, Neutral, the Shadow Ninja at 389, Evil",
              string.Join("; ", shared.Select(g => g.Key + ": " + string.Join(", ", g.Select(m => $"{m.Name.Trim()} {m.Ref} {m.AlignmentValue}")))),
              "307: Morgul 278 1, Shadow Ninja 389 2");
        var morgul = monsters.First(m => m.Name.Trim() == "Morgul");
        var ninja = monsters.First(m => m.Name.Trim() == "Shadow Ninja");
        var morloch = monsters.First(m => m.Name.Trim() == "Morloch");

        // ---- a companion's record ----
        string Name(Companion c) => c.Def(monsters)?.Name.Trim() ?? "none";
        var slots = new CompanionSlots();
        var sn = CompanionRef.Of(ninja); sn.Hits = 9; sn.MaxHits = 9; sn.Alignment = ItemAlignment.Neutral;
        slots.Add(sn);
        string json = JsonSerializer.Serialize(slots);
        var back = JsonSerializer.Deserialize<CompanionSlots>(json)!;
        Check("a Shadow Ninja realigned Neutral is saved with its Ref and read back the Shadow Ninja",
              (json.Contains("\"Ref\":389") && json.Contains("\"MonsterId\":307"), back[0]!.Ref, Name(back[0]!), back.Settle(monsters)).ToString(),
              "(True, 389, Shadow Ninja, 0)");

        CompanionSlots Legacy(string body) => JsonSerializer.Deserialize<CompanionSlots>("[" + body + "]")!;
        var legacy = Legacy("{\"MonsterId\":307,\"Alignment\":2,\"Hits\":5,\"MaxHits\":5}," +     // Neutral
                            "{\"MonsterId\":307,\"Alignment\":3,\"Hits\":5,\"MaxHits\":5}," +     // Evil
                            "{\"MonsterId\":307,\"Alignment\":1,\"Hits\":5,\"MaxHits\":5}," +     // Good
                            "{\"MonsterId\":307,\"Alignment\":0,\"Hits\":5,\"MaxHits\":5}");      // Unaligned
        Check("an old save's companions have no Ref", string.Join(",", legacy.All.Select(c => c.Ref)), "-1,-1,-1,-1");
        int settled = legacy.Settle(monsters);
        Check("Id 307 settled: Neutral the Morgul, Evil the Shadow Ninja, Good and Unaligned the first index with it (MAINMOD2.BAS:4722-4727)",
              (settled, string.Join(",", legacy.All.Select(c => $"{c.Ref} {Name(c)}"))).ToString(),
              "(4, 278 Morgul,389 Shadow Ninja,278 Morgul,278 Morgul)");
        Check("... the Unaligned one then takes the Morgul's own alignment, the realigned Good one keeps Good",
              string.Join(",", legacy.All.Select(c => c.Alignment)), "Neutral,Evil,Good,Neutral");
        var plain = Legacy($"{{\"MonsterId\":{morloch.Id},\"Alignment\":3,\"Hits\":5,\"MaxHits\":5}},{{\"MonsterId\":32000,\"Hits\":5,\"MaxHits\":5}}");
        plain.Settle(monsters);
        Check("an Id that is one creature converts exactly, whatever its alignment; an Id no creature has finds none",
              (plain[0]!.Ref == morloch.Ref, plain[1]!.Ref, Name(plain[1]!)).ToString(), "(True, -1, none)");
        Check("settled once, settled for good", legacy.Settle(monsters) + plain.Settle(monsters), 0);
        var stale = new CompanionSlots();
        stale.Add(new Companion { Ref = morloch.Ref, MonsterId = 307, Hits = 5, MaxHits = 5, Alignment = ItemAlignment.Evil });
        stale.Settle(monsters);
        Check("a Ref naming a creature of another Id goes to the first index with the Id, as the sorter does", stale[0]!.Ref, 278);

        var bare = new CompanionSlots();
        bare.Add(new Companion { Ref = ninja.Ref, Hits = 5, MaxHits = 5, Alignment = ItemAlignment.Evil });
        Check("a record given only its Ref keeps it, not sent to the creature of Id 0", (bare.Settle(monsters), Name(bare[0]!)).ToString(), "(0, Shadow Ninja)");

        // ---- the pens ----
        var pens = new Confinement();
        pens.Add(morgul, ItemAlignment.Neutral);
        pens.Add(ninja, ItemAlignment.Evil);
        pens.Add(ninja, ItemAlignment.Neutral);
        string Gne(Monster m) => string.Join(" ", Enumerable.Range(1, 3).Select(a => pens.Count(m, (ItemAlignment)a)));
        Check("the Morgul's and the Shadow Ninja's pens are their own", $"{Gne(morgul)} | {Gne(ninja)}", "0 1 0 | 0 1 1");
        Check("... keyed by table index", string.Join(",", pens.Pens.Keys.OrderBy(k => k)), "278:2,389:2,389:3");
        Check("... and priced apart: each its own worth over its own stock",
              (pens.PriceToBuy(morgul, ItemAlignment.Neutral) == (long)Math.Round(1.5 * Confinement.BaseValue(morgul) / 1.15, MidpointRounding.ToEven),
               pens.PriceToBuy(ninja, ItemAlignment.Neutral) == (long)Math.Round(1.5 * Confinement.BaseValue(ninja) / 1.15, MidpointRounding.ToEven)).ToString(),
              "(True, True)");

        var handMade = new Monster { Name = "Nobody", Hits = 5, LevelFound = 3 };
        pens.Add(handMade, ItemAlignment.Neutral);
        pens.Take(handMade, ItemAlignment.Neutral);
        Check("a creature with no table index has no pen: none counted, nothing added or taken", (pens.Count(handMade, ItemAlignment.Neutral), pens.Pens.Count).ToString(), "(0, 3)");

        // bought, sold, turned: the creature the Ref names
        var buyer = Character.Create("b", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        var bought = CompanionRef.Of(ninja);   // cmdBuy_Click: Ref = l00B4, the row's index
        bought.Hits = ninja.Hits; bought.MaxHits = ninja.Hits; bought.Alignment = ItemAlignment.Neutral;
        buyer.Companions.Add(bought);
        pens.Take(ninja, ItemAlignment.Neutral);
        Check("a Shadow Ninja bought out of its Neutral pen: the Morgul's is untouched", $"{Gne(morgul)} | {Gne(ninja)} | {Name(bought)}", "0 1 0 | 0 0 1 | Shadow Ninja");
        pens.Add(bought.Def(monsters)!, bought.Alignment);
        Check("... sold back, into the Shadow Ninja's pen", $"{Gne(morgul)} | {Gne(ninja)}", "0 1 0 | 0 1 1");
        CompanionTurn.Outcome Turn(Monster creature)
        {
            var master = Character.Create("t", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var pet = CompanionRef.Of(creature);
            pet.Hits = 9; pet.MaxHits = 9; pet.BindingLevel = 5; pet.Alignment = ItemAlignment.Neutral;
            master.Companions.Add(pet);
            return CompanionTurn.Check(master, 0, false, new ScriptedRandom(new[] { 0.5, 0.9 }), c => c.Def(monsters), null, isLeader: true, groupsPresent: 0);
        }
        var tm = Turn(morgul);
        var tn = Turn(ninja);
        Check("a Neutral master, rolls 50 and 90: the Morgul (Neutral) stays, the Shadow Ninja (Evil, 50 < 75) turns - same Id, its own creature",
              (tm.Turned, tn.Turned, tn.Became?.Def.Name.Trim()).ToString(), "(False, True, Shadow Ninja)");
        var who = Character.Create("j", RaceData.All[0], Alignment.Evil, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        EncounterGenerator.AcceptJoin(who, new List<CombatMonster> { new() { Def = ninja, Hits = 9, MaxHits = 9, JoinOffer = 1, Group = 0, Alignment = 2 } }, _ => null);
        Check("a Shadow Ninja that joins carries its Ref (fn1163, MAINMOD2.BAS:388)", (who.Companions[0]?.Ref, who.Companions[0]?.MonsterId).ToString(), "(389, 307)");

        // ---- an old confinement.json ----
        string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mordor-selfcheck-penref-" + Environment.ProcessId);
        try
        {
            System.IO.Directory.CreateDirectory(tmp);
            string file = System.IO.Path.Combine(tmp, "confinement.json");
            System.IO.File.WriteAllText(file, "{\"Pens\":{\"307:2\":3,\"307:3\":1,\"307:1\":2,\"307:0\":1,\"" + morloch.Id + ":2\":4,\"" + morloch.Id + ":0\":2,\"32000:2\":5}}");
            var loaded = Confinement.Load(tmp, monsters);
            string Has(Confinement p, Monster m) => string.Join(" ", Enumerable.Range(1, 3).Select(a => p.Count(m, (ItemAlignment)a)));
            Check("an Id-keyed file: Neutral 307 to the Morgul, Evil to the Shadow Ninja, Good and Unaligned to the first index with the Id, the Morgul (MAINMOD2.BAS:4722-4727), the Unaligned one then at its own",
                  $"{Has(loaded, morgul)} | {Has(loaded, ninja)}", "2 4 0 | 0 0 1");
            Check("... and not one creature made up: seven 307s before, seven after",
                  Enumerable.Range(1, 3).Sum(a => loaded.Count(morgul, (ItemAlignment)a) + loaded.Count(ninja, (ItemAlignment)a)), 7);
            Check("... a creature of its own Id moves exactly, its Unaligned two to its own pen; an Id no creature has is dropped",
                  (Has(loaded, morloch), loaded.Pens.Count, loaded.KeyedBy).ToString(), "(0 6 0, 4, Ref)");
            loaded.Save(tmp);
            Check("written back keyed by table index", System.IO.File.ReadAllText(file).Contains("\"KeyedBy\": \"Ref\""), true);
            var again = Confinement.Load(tmp, monsters);
            Check("... and read back as it was, not moved twice",
                  string.Join(",", again.Pens.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value)),
                  string.Join(",", loaded.Pens.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value)));
            System.IO.File.Delete(file);
            var fresh = Confinement.Load(tmp, monsters);
            var range = EncounterGenerator.ClassRanges(monsters);
            var expect = new[] { range[0, 0], range[0, 0] + 1, range[7, 0], range[7, 0] + 1, range[8, 0], range[8, 0] + 1,
                                 range[14, 0], range[14, 0] + 1, range[1, 0], range[9, 0] }.OrderBy(i => i);
            Check("a new game's pens: sub054E's ten table indices, two each at the creature's own (MAINMOD.BAS:544-551)",
                  (string.Join(",", fresh.Entries.OrderBy(e => e.Ref).Select(e => e.Ref)) == string.Join(",", expect),
                   fresh.Entries.All(e => e.Count == 2 && e.Align == Confinement.AlignmentOf(monsters[e.Ref])), fresh.KeyedBy).ToString(),
                  "(True, True, Ref)");
        }
        finally { try { System.IO.Directory.Delete(tmp, true); } catch { } }

        // ---- the port's wiring: no companion looked up by its Id ----
        string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs"), cityMdi = PortSource("src/Mordor.App", "CityWindow.Mdi.cs"),
               dun = PortSource("src/Mordor.App", "DungeonWindow.axaml.cs");
        Check("the windows look companions up by Ref, not by Id",
              new[] { city, cityMdi, dun }.All(s => !s.Contains("== c.MonsterId") && !s.Contains("== comp.MonsterId") && !s.Contains("MonsterById")
                                                   && !System.Text.RegularExpressions.Regex.IsMatch(s, @"pen\.(Count|Take|AnyOf)\([^)]*\.Id\b")), true);
        Check("the Confinement's Buy fills Ref with the row's index", city.Contains("Ref = idx, MonsterId = def.Id,"), true);
        string conf = PortSource("src/Mordor.Core", "Confinement.cs");
        Check("the pens are only read with the table, and a store still keyed by Id is never written back as keyed by Ref",
              conf.Contains("public static Confinement Load(string savesDir, IReadOnlyList<Monster> monsters)") && conf.Contains("if (_idKeyed) return;"), true);
    }
}
