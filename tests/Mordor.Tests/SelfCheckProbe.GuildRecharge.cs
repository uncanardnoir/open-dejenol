using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// Guild items recharged on entering the city (2026-10-31). The city's Form_Load ends each
    /// character's turn with <c>sub15F7</c> (FRMCTYM.BAS:133), which fills every guild-issued item
    /// in pack slots 1 to 39 back up to the item's own charges and says so (MAINMOD2.BAS:4250-4271).
    /// The port had no recharge at all: a Crest of the Thief used down stayed down.
    /// </summary>
    private static void GuildRechargeChecks(string dir)
    {
        Console.WriteLine("\nguild items recharged on entering the city (MAINMOD2.BAS:4250-4271, FRMCTYM.BAS:133)");
        string Norm(string s) => s.Replace("\r\n", "\n");
        string main2 = Norm(PortSource("decompiled-named", "MAINMOD2.BAS"));
        string ctym = Norm(PortSource("decompiled-named", "FRMCTYM.BAS"));

        // ---- the original ----
        Check("MAINMOD2.BAS: sub15F7 looks at slots 1 to 39 and fills a guild item below its charges",
              main2.Contains("    For l0654 = 1 To mc0190 - 1\n        If  Chars(pv0652).Pack(l0654).Ref > 0 Then\n"
                           + "            If  Items(Chars(pv0652).Pack(l0654).Ref).Ailments >= 500 And Items(Chars(pv0652).Pack(l0654).Ref).Ailments <= 500 + (NumGuilds - 1) Then\n"
                           + "                If  Chars(pv0652).Pack(l0654).Charges < Items(Chars(pv0652).Pack(l0654).Ref).Charges Then\n"
                           + "                    l0656 = True\n                    Chars(pv0652).Pack(l0654).Charges = Items(Chars(pv0652).Pack(l0654).Ref).Charges")
              && main2.Contains("Const mc0190 = 40 ' &H28%") && main2.Contains("Const m00A6 = 0 ' &H0%"), true);
        Check("MAINMOD2.BAS: its two lines, alone and in a party",
              main2.Contains("        If  UBound(Chars) = m00A6 Then\n            sub085B \"Your Guild Item(s) were recharged when you entered the city.\"\n        Else\n"
                           + "            sub085B Chr(34) + RTrim(Chars(pv0652).Name) + Chr(34) + \" had their Guild Item(s) recharged when they entered the city.\""), true);
        Check("FRMCTYM.BAS: Form_Load calls it for everyone, last in each one's turn, after the save",
              ctym.Contains("    For gv1A84 = m0040 To UBound(Chars)\n")
              && ctym.Contains("        If  fn04ED(Chars(gv1A84)) = False Then sub04FC \"Error saving character in FRMCTYM:Form_Load!\"\n"
                             + "        Chars(gv1A84).M59BB = -1\n        sub15F7 gv1A84\n    Next gv1A84"), true);
        string city = PortSource("src/Mordor.App", "CityWindow.axaml.cs");
        Check("the port: RestOnArrival recharges each member after their carried body is left",
              city.Contains("m.CarryingCharacter = \"\";\n            }\n") && city.Contains("if (ItemUse.RechargeGuildItems(m, party, Lookup) is string recharged) lines.Add(recharged);"), true);

        // ---- the data ----
        if (!System.IO.File.Exists(System.IO.Path.Combine(dir, "MDATA3.MDR"))) { Console.WriteLine("  skipped: no MDATA3.MDR"); return; }
        var items = MdrLoader.LoadItems(System.IO.Path.Combine(dir, "MDATA3.MDR"));
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        Item? Look(short id) => byId.TryGetValue(id, out var it) ? it : null;
        Check("MDATA3: the guild items (Ailments 500 + guild), with their charges",
              string.Join("; ", items.Where(ItemUse.IsGuildIssued).Select(i => $"{i.Name.Trim()} {i.SpecialType} {i.Charges}")),
              "Crest of the Nomad 500 0; Crest of the Warrior 501 0; Crest of the Paladin 502 12; Crest of the Ninja 503 0; "
              + "Crest of the Villain 504 6; Crest of the Seeker 505 6; Crest of the Thief 506 12; Crest of the Scavenger 507 6; "
              + "Crest of the Mage 508 6; Crest of the Sorcerer 509 6; Crest of the Wizard 510 6; Crest of the Healer 511 6");
        var thief = items.First(i => i.Name.Trim() == "Crest of the Thief");
        var mage = items.First(i => i.Name.Trim() == "Crest of the Mage");
        // an ordinary item that has charges: the first one MDATA3 has that is not a guild item
        var wand = items.First(i => !ItemUse.IsGuildIssued(i) && i.Charges > 1 && i.Id > 0);

        // ---- sub15F7 ----
        Character Carrying(string name, params (int slot, Item it, int charges)[] pack)
        {
            var c = new Character { Name = name };
            foreach (var (slot, it, charges) in pack) c.Pack[slot] = new InventorySlot { ItemId = it.Id, Charges = (short)charges };
            return c;
        }
        var alone = Carrying("Ann", (3, thief, 4));
        Check("alone: a Crest of the Thief at 4 is filled to 12, \"Your Guild Item(s) ...\"",
              (ItemUse.RechargeGuildItems(alone, false, Look), alone.Pack[3]!.Charges).ToString(),
              "(Your Guild Item(s) were recharged when you entered the city., 12)");
        var named = Carrying("Bob   ", (7, thief, 0), (9, mage, 2));
        Check("in a party: both crests filled, one line with the trimmed name in quotes",
              (ItemUse.RechargeGuildItems(named, true, Look), named.Pack[7]!.Charges, named.Pack[9]!.Charges).ToString(),
              "(\"Bob\" had their Guild Item(s) recharged when they entered the city., 12, 6)");
        var full = Carrying("Cid", (2, thief, 12), (5, wand, 0));
        Check("a full crest is not touched and nothing is said; an ordinary item with charges is not recharged",
              (ItemUse.RechargeGuildItems(full, false, Look) ?? "null", full.Pack[2]!.Charges, full.Pack[5]!.Charges).ToString(),
              "(null, 12, 0)");
        var over = Carrying("Dee", (4, thief, 15));
        Check("above full is left as it is (only Pack charges < Items charges is filled)",
              (ItemUse.RechargeGuildItems(over, false, Look) ?? "null", over.Pack[4]!.Charges).ToString(), "(null, 15)");
        var hands = new Character { Name = "Eve" };
        hands.Pack[0] = new InventorySlot { ItemId = thief.Id, Charges = 1 };
        Check("slot 0 is not looked at: the loop starts at 1",
              (ItemUse.RechargeGuildItems(hands, false, Look) ?? "null", hands.Pack[0]!.Charges).ToString(), "(null, 1)");
        var last = Carrying("Fay", (Character.PackSize - 1, thief, 0));
        Check("slot 39 is: the loop runs to mc0190 - 1",
              (ItemUse.RechargeGuildItems(last, false, Look) != null, last.Pack[Character.PackSize - 1]!.Charges).ToString(), "(True, 12)");
    }
}
