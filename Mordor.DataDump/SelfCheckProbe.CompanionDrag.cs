using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// Two companion reports (2026-10-18). Drag and drop did nothing for companions: the port had no
    /// companion to drag, so neither rearranging the Companions page (picCompanion_DragDrop,
    /// FRMCHAR3.BAS:763-779) nor dropping one on the Confinement's Comp. boxes
    /// (FRMCITYC.BAS:744-796) could happen. And two Morlochs sold to the Confinement showed as
    /// "0 0 0": the charm and the join never copied the foe's alignment into the companion
    /// (fn1163, MAINMOD2.BAS:392), so they were Unaligned and sold into a pen no column shows.
    /// </summary>
    private static void CompanionDragChecks(string dir)
    {
        Console.WriteLine("\ncompanions: dragged and rearranged (FRMCHAR3.BAS:763-799), and penned at their alignment (MAINMOD2.BAS:392)");
        string Norm(string s) => s.Replace("\r\n", "\n");
        string char3 = Norm(PortSource("decompiled-named", "FRMCHAR3.BAS"));
        string cityc = Norm(PortSource("decompiled-named", "FRMCITYC.BAS"));
        string main2 = Norm(PortSource("decompiled-named", "MAINMOD2.BAS"));
        string mons = Norm(PortSource("decompiled-named", "MONSMOD.BAS"));

        // ---- the original ----
        Check("FRMCHAR3.BAS: a picture is dragged while a button is down over it",
              char3.Contains("Sub picCompanion_MouseMove (Index%, Button As Integer, Shift As Integer, X As Single, Y As Single)\n    If  Button Then\n        picCompanion(Index).Drag 1"), true);
        Check("FRMCHAR3.BAS: dropped on another, the two records change places, unless either is dead",
              char3.Contains("    If  Source.Tag = \"COMP\" Then\n        If  Index <> Source.Index Then\n            If  Chars(Me).Companions(Index).Hits = 0 Then\n                sub085B \"No use re-arranging a dead companion!\"")
              && char3.Contains("            l02B0 = Chars(Me).Companions(Index)\n            Chars(Me).Companions(Index) = Chars(Me).Companions(Source.Index)\n            Chars(Me).Companions(Source.Index) = l02B0"), true);
        Check("FRMCITYC.BAS: on the Bind and ID boxes, the slot's number and Enter",
              cityc.Contains("    If  Source.Tag = \"COMP\" Then\n        txtBind.Text = Source.Index + 1\n        txtBind_KeyPress 13")
              && cityc.Contains("    If  Source.Tag = \"COMP\" Then\n        txtID.Text = Source.Index + 1\n        txtID_KeyPress 13"), true);
        Check("MAINMOD2.BAS: fn1163 gives the companion the foe's alignment",
              main2.Contains("Chars(pv00D2).Companions(l00DC).Alignment = p00D6.Alignment"), true);
        Check("MONSMOD.BAS: and the foe has the creature's own",
              mons.Contains("Foes(m0286).Alignment = Monsters(m0284).Alignment"), true);
        Check("FRMCITYC.BAS: a sale pens it under the companion's alignment",
              cityc.Contains("gv162E(Chars(Me).Companions(l0126).Ref).Align(Chars(Me).Companions(l0126).Alignment) = gv162E(Chars(Me).Companions(l0126).Ref).Align(Chars(Me).Companions(l0126).Alignment) + 1"), true);

        // ---- rearranging ----
        var slots = new CompanionSlots();
        slots.Add(new Companion { MonsterId = 1, Name = "A", Hits = 5, MaxHits = 5 });
        slots.Add(new Companion { MonsterId = 2, Name = "B", Hits = 7, MaxHits = 7 });
        slots.Add(new Companion { MonsterId = 3, Name = "C", Hits = 0, MaxHits = 9 });
        string Order() => string.Join(",", Enumerable.Range(0, CompanionSlots.Slots).Select(i => slots[i]?.Name ?? "-"));
        Check("B dropped on A: they change places", (slots.Rearrange(0, 1), Order()).ToString(), "(, B,A,C,-)");
        Check("dropped on itself: nothing", (slots.Rearrange(1, 1), Order()).ToString(), "(, B,A,C,-)");
        Check("a live one dropped on the dead one: refused", (slots.Rearrange(2, 0), Order()).ToString(), $"({CompanionSlots.NoRearrangingTheDead}, B,A,C,-)");
        Check("the dead one dropped on a live one: refused", (slots.Rearrange(1, 2), Order()).ToString(), $"({CompanionSlots.NoRearrangingTheDead}, B,A,C,-)");
        Check("an empty slot has no picture to drop on", (slots.Rearrange(3, 0), Order()).ToString(), "(, B,A,C,-)");
        Check("the line is the original's", CompanionSlots.NoRearrangingTheDead, "No use re-arranging a dead companion!");

        // ---- the alignment a companion arrives with ----
        var monsters = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var morloch = monsters.FirstOrDefault(m => m.Name.Trim() == "Morloch");
        if (morloch == null) { Console.WriteLine("  FAIL  no Morloch in MDATA5"); _failed++; return; }
        Check("the Morloch is Neutral (MDATA5) and found on level 2", (morloch.AlignmentValue, morloch.LevelFound).ToString(), "(1, 2)");

        var who = Character.Create("j", RaceData.All[0], Alignment.Good, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        var grp = new List<CombatMonster>
        {
            new() { Def = morloch, Hits = 9, MaxHits = 9, JoinOffer = 2, Group = 0, Alignment = morloch.AlignmentValue },
            new() { Def = morloch, Hits = 9, MaxHits = 9, Group = 0, Alignment = morloch.AlignmentValue },
        };
        EncounterGenerator.AcceptJoin(who, grp, _ => null);
        Check("two Morlochs join: both Neutral, the foe's alignment (they had been Unaligned)",
              string.Join(",", who.Companions.All.Select(c => c.Alignment)), "Neutral,Neutral");
        var odd = new CombatMonster { Def = morloch, Hits = 9, Alignment = 2 };
        Check("a foe carrying another alignment (a turned companion, MONSMOD.BAS:569) passes that on", odd.CompanionAlignment, ItemAlignment.Evil);
        Check("a hand-built one with none reads as the creature's own", new CombatMonster { Def = morloch, Hits = 9 }.CompanionAlignment, ItemAlignment.Neutral);

        // ---- sold: into the Neutral pen, which the list shows ----
        var pens = new Confinement();
        foreach (var c in who.Companions.All) pens.Add(morloch, c.Alignment);
        string Row(Confinement p) => string.Join(" ", Enumerable.Range(0, 3).Select(a => Math.Min(p.Count(morloch, (ItemAlignment)(a + 1)), Confinement.ShelfCap)));
        Check("both sold: the list's G N E reads 0 2 0", Row(pens), "0 2 0");

        // ---- what the old port left behind (pens keyed by table index; the Id-keyed files are
        // the CompanionRef checks') ----
        var first = monsters[1];
        var old = new Confinement();
        old.Pens[$"{morloch.Ref}:0"] = 2;
        old.Pens[$"{first.Ref}:{(int)Confinement.AlignmentOf(first)}"] = 9;
        old.Pens[$"{first.Ref}:0"] = 3;
        Check("before: the two Morlochs are in no column", Row(old), "0 0 0");
        int moved = old.RepairUnaligned(monsters);
        Check("the Unaligned pens go to the creature's own alignment", (moved, Row(old)).ToString(), "(5, 0 2 0)");
        Check("... capped at nine, as a sale is (FRMCITYC.BAS:288)", old.Count(first, Confinement.AlignmentOf(first)), 9);
        Check("... and none is left Unaligned", old.Pens.Keys.Any(k => k.EndsWith(":0")), false);

        var held = new CompanionSlots();
        held.Add(new Companion { Ref = morloch.Ref, MonsterId = morloch.Id, Hits = 9, MaxHits = 9 });
        held.Add(new Companion { Ref = morloch.Ref, MonsterId = morloch.Id, Hits = 9, MaxHits = 9, Alignment = ItemAlignment.Evil });
        int fixedUp = held.Settle(monsters);
        Check("a companion saved Unaligned takes the creature's own; a realigned one keeps its own",
              (fixedUp, string.Join(",", held.All.Select(c => c.Alignment))).ToString(), "(1, Neutral,Evil)");

        // ---- turning on you, monCheckCompAttack (MONSMOD.BAS:536-554) ----
        var good = monsters.First(m => m.Id != 0 && m.Id != 307 && m.AlignmentValue == 0);
        var evil = monsters.First(m => m.Id != 0 && m.Id != 307 && m.AlignmentValue == 2);
        CompanionTurn.Outcome Turn(Monster creature, ItemAlignment own, double[] rolls, out int draws,
                                   bool forced = false, int binding = 5, List<CombatMonster>? fight = null)
        {
            var master = Character.Create("t", RaceData.All[0], Alignment.Good, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            var pet = CompanionRef.Of(creature);
            pet.Hits = 9; pet.MaxHits = 9; pet.BindingLevel = binding; pet.Alignment = own;
            master.Companions.Add(pet);
            var rng = new ScriptedRandom(rolls);
            var o = CompanionTurn.Check(master, 0, forced, rng, c => c.Def(monsters), fight, isLeader: true, groupsPresent: 0);
            draws = rng.Draws;
            return o;
        }
        Check("MONSMOD.BAS: the chances read Monsters(l0178).Alignment, the creature's own, with a Rnd on each line",
              mons.Contains("    l0178 = Chars(pv016E).Companions(p0170).Ref\n    If  Abs(Monsters(l0178).Alignment - Chars(pv016E).Alignment) > 1 Then l017A = True\n    If  Monsters(l0178).Alignment = mc0180 And (Rnd * 100) < 75 Then l017A = True\n    If  Monsters(l0178).Alignment = mc0182 And (Rnd * 100) < 15 Then l017A = True"), true);
        Check("MONSMOD.BAS: the refusal reads Foes(0).Alignment against the creature's own",
              mons.Contains("If  Abs(Foes(0).Alignment - Monsters(l0178).Alignment) > 1 Then"), true);
        var t1 = Turn(morloch, ItemAlignment.Evil, new[] { 0.5, 0.9 }, out int d1);
        Check("a Neutral creature realigned Evil, rolls 50 and 90: it does not turn - the creature is Neutral, and 90 is not under 15",
              (t1.Turned, d1).ToString(), "(False, 2)");
        var t2 = Turn(good, ItemAlignment.Good, new[] { 0.1, 0.1 }, out int d2);
        Check("a Good creature of a Good master: no chance, and still both Rnd are drawn (VB's And does not short-circuit)", (t2.Turned, d2).ToString(), "(False, 2)");
        Turn(good, ItemAlignment.Good, new[] { 0.9, 0.9 }, out int d3, forced: true);
        Check("attacked (forced): both Rnd are drawn too", d3, 2);
        Turn(good, ItemAlignment.Good, new[] { 0.9, 0.9 }, out int d4, binding: -10);
        Check("one that came of its own free will, kicked out: it leaves before any Rnd (536-538)", d4, 0);
        var t5 = Turn(morloch, ItemAlignment.Neutral, new[] { 0.9, 0.1 }, out _);
        Check("a Neutral creature, second roll 10: it turns", t5.Turned, true);
        var gather = new List<CombatMonster> { new() { Def = evil, Hits = 9, Group = 0, Alignment = 0 } };
        var t6 = Turn(good, ItemAlignment.Good, new[] { 0.9, 0.9 }, out _, forced: true, fight: gather);
        Check("a Good creature among foes whose slot 0 is Good (an Evil creature's group carrying Good): no refusal", t6.Refused, false);
        gather[0].Alignment = 2;
        var t7 = Turn(good, ItemAlignment.Good, new[] { 0.9, 0.9 }, out _, forced: true, fight: gather);
        Check("... and with slot 0 Evil it refuses to join them", (t7.Refused, t7.Message).ToString(), "(True, Your companion refuses to join the current monsters!)");
        var t8 = Turn(morloch, ItemAlignment.Good, new[] { 0.9, 0.9 }, out _, forced: true, fight: gather);
        Check("... a Neutral creature realigned Good is not refused by Evil foes: the creature's own is read", t8.Refused, false);

        // ---- the port's wiring ----
        string combat = PortSource("Mordor.Core", "Combat.cs");
        Check("the charm and the join copy the foe's alignment, and a group's members carry their slot's",
              combat.Contains("Alignment = m.CompanionAlignment,") && combat.Contains("Alignment = front.CompanionAlignment,")
              && combat.Contains("Alignment = s.Alignment,") && !combat.Contains("is not tracked by this port"), true);
        Check("a turned companion carries its own (MONSMOD.BAS:569)",
              PortSource("Mordor.Core", "CompanionTurn.cs").Contains("Alignment = MonsterAlignment(def, c),"), true);
        string cityMdi = PortSource("Mordor.App", "CityWindow.Mdi.cs"), dunMdi = PortSource("Mordor.App", "DungeonWindow.Mdi.cs");
        Check("both Companions pages drag and take companions",
              cityMdi.Contains("CompanionDrag.Wire(_companionPage.Pictures, () => _ch.Companions, Log,")
              && dunMdi.Contains("CompanionDrag.Wire(_companionPage.Pictures, () => _ch.Companions, Say,"), true);
        string city = PortSource("Mordor.App", "CityWindow.axaml.cs"), dun = PortSource("Mordor.App", "DungeonWindow.axaml.cs");
        Check("the Confinement's Bind and ID boxes take a dropped companion and press Enter",
              city.Contains("TakeDrops(txtBind, BindEnter);") && city.Contains("TakeDrops(txtID, IdEnter);")
              && city.Contains("box.Text = (p.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);"), true);
        Check("the name opens its box on the click (lblCompName_Click), and the picture is what is dragged (picCompanion_MouseMove)",
              PortSource("Mordor.App", "CompanionsPage.cs") is var page && !page.Contains("label.PointerPressed")
              && page.Contains("label.Tapped += (_, e) =>")
              && PortSource("Mordor.App", "DragPayload.cs").Contains("Dragging.Source(picture, () => slots()[onto] != null ? DragPayload.Companion(onto) : null);"), true);
        Check("FRMCHAR3.BAS: the right-click refuses when both alignments are m02BE, a Static never assigned (0, Good)",
              char3.Contains("Static m02BE As Integer") && !char3.Contains("m02BE =") && char3.Contains("sub085B \"It is against your ways to attack a Good companion.\""), true);
        Check("the dungeon's Attack refuses a Good character's Good companion with that line",
              dun.Contains("if (_ch.Alignment == Alignment.Good && _ch.Companions[i]!.Alignment == ItemAlignment.Good)")
              && dun.Contains("Say(NoAttackingGoodCompanions);")
              && dun.Contains("NoAttackingGoodCompanions = \"It is against your ways to attack a Good companion.\";"), true);
        Check("a companion let go over a party name gives that member the commands (FRMGROUP.BAS:214-220)",
              city.Contains("Dragging.Target(row, DragKind.Companion, _ => SwitchIfOther(slot));")
              && dun.Contains("Dragging.Target(row, DragKind.Companion, _ =>"), true);
    }
}

/// <summary>A Random that answers the doubles given, in order, and counts how many were drawn.</summary>
sealed class ScriptedRandom : Random
{
    private readonly double[] _rolls;
    public int Draws { get; private set; }
    public ScriptedRandom(double[] rolls) { _rolls = rolls; }
    public override double NextDouble() => _rolls[Math.Min(Draws++, _rolls.Length - 1)];
    protected override double Sample() => NextDouble();
    public override int Next(int maxValue) => (int)(NextDouble() * maxValue);
    public override int Next(int minValue, int maxValue) => minValue + (int)(NextDouble() * (maxValue - minValue));
    public override int Next() => (int)(NextDouble() * int.MaxValue);
}
