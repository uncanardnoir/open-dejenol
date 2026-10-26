using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// The alignment choice of the Store's and the Confinement's Buy lists (AlignmentChoice): the
    /// buttons offered by stock, one alignment taken without asking, the frame only for two or
    /// more, and the Confinement's frame hidden again once one is picked - it had been left up.
    /// </summary>
    private static void AlignmentChoiceChecks(string dir)
    {
        Console.WriteLine("\nthe Store's and the Confinement's alignment choice (FRMCTYS.BAS:326-392, FRMCITYC.BAS:302-370)");
        string S(AlignmentChoice.Offer o) => string.Concat(o.Enabled.Select(e => e ? '1' : '0')) + $" {o.Stocked} {o.Last}";

        // the original: both loops count the enabled buttons, keep the last, and ask only past one
        string cstore = PortSource("decompiled-named", "FRMCITYC.BAS").Replace("\r\n", "\n").Replace("\n", "\r\n");
        string store = PortSource("decompiled-named", "FRMCTYS.BAS").Replace("\r\n", "\n").Replace("\n", "\r\n");
        Check("FRMCITYC.BAS: the loop leaves m002C at the last stocked alignment, and asks only If l0144% > 1",
              cstore.Contains("l0144% = l0144% + 1\r\n            m002C = l0140") && cstore.Contains("If  l0144% > 1 Then\r\n        m002C% = -1\r\n        elaAlignments.P?C008 = True"), true);
        Check("FRMCITYC.BAS: the frame goes down after the wait, and with one alignment (P?C008 = False)",
              cstore.Contains("cmdInfo.Enabled = True\r\n        elaAlignments.P?C008 = False\r\n    Else\r\n        cmdBuy.Enabled = True\r\n        cmdInfo.Enabled = True\r\n        elaAlignments.P?C008 = False"), true);
        Check("FRMCITYC.BAS: the loop is G, N and E (0 to (mc00D2 - 1) - 1)", cstore.Contains("For l0140 = 0 To (mc00D2 - 1) - 1"), true);
        Check("FRMCTYS.BAS: the Store's is the same, over U G N E",
              store.Contains("l0168% = l0168% + 1\r\n            m0028% = l0164") && store.Contains("If  l0168% > 1 Then\r\n        m0028% = -1\r\n        elaAlignments.P?C008 = True")
              && store.Contains("For l0164 = 0 To mc009C - 1"), true);

        // the decision
        var one = AlignmentChoice.For(new[] { 0, 2, 0 });
        Check("Confinement, only N penned: N offered alone", S(one), "010 1 1");
        Check("... not asked", one.Asks, false);
        Check("... N taken at once, whatever was chosen before", one.Chosen(2), 1);
        var two = AlignmentChoice.For(new[] { 1, 0, 3 });
        Check("Confinement, G and E penned: G and E offered", S(two), "101 2 2");
        Check("... asked: m002C is -1 until a button is pressed", (two.Asks, two.Chosen(0)).ToString(), "(True, -1)");
        var none = AlignmentChoice.For(new[] { 0, 0, 0 });
        Check("a row with nothing left: nothing offered, the alignment before stands", (S(none), none.Asks, none.Chosen(2)).ToString(), "(000 0 -1, False, 2)");
        var evil = AlignmentChoice.For(new[] { 0, 0, 0, 5 });
        Check("Store, only E shelved: E taken at once (index 3 of U G N E)", (evil.Asks, evil.Chosen(0)).ToString(), "(False, 3)");
        var ug = AlignmentChoice.For(new[] { 4, 1, 0, 0 });
        Check("Store, U and G shelved: asked", (S(ug), ug.Asks).ToString(), "(1100 2 1, True)");

        // the chosen alignment is the one priced and bought: a Confinement pen holding one creature
        // in Neutral only
        var beast = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR")).FirstOrDefault(m => m.Id != 0 && m.LevelFound > 0);
        if (beast == null) { Console.WriteLine("  FAIL  no creature to pen"); _failed++; }
        else
        {
            var pens = new Confinement();
            pens.Add(beast, ItemAlignment.Neutral);
            pens.Add(beast, ItemAlignment.Neutral);
            var offer = AlignmentChoice.For(Enumerable.Range(0, 3).Select(a => pens.Count(beast, (ItemAlignment)(a + 1))).ToList());
            var align = (ItemAlignment)(offer.Chosen(0) + 1);
            Check($"a pen of two Neutral {beast.Name.Trim()}: Neutral chosen without asking", (offer.Asks, align).ToString(), "(False, Neutral)");
            Check("... priced at Neutral's stock, not at Good's empty pen", pens.PriceToBuy(beast, align) > 0 && pens.PriceToBuy(beast, ItemAlignment.Good) == 0, true);
            pens.Take(beast, align);
            Check("... and bought out of Neutral's pen", $"{pens.Count(beast, ItemAlignment.Neutral)} {pens.Count(beast, ItemAlignment.Good)}", "1 0");
        }

        // the port's wiring: both row clicks go through the decision, and the Confinement's frame is
        // up only while it waits (it had been shown on every click and never hidden)
        string city = PortSource("Mordor.App", "CityWindow.axaml.cs");
        string shop = PortSource("Mordor.App", "CityWindow.Store.cs");
        string click = Between(city, "async void ClickRow(int p)", "// cmdAlign_Click (FRMCITYC.BAS:50-52)");
        Check("Confinement: the row click asks AlignmentChoice and keeps its answer",
              click.Contains("var offer = AlignmentChoice.For(") && click.Contains("chosenAlign = offer.Chosen(chosenAlign);") && click.Contains("if (offer.Asks)"), true);
        Check("Confinement: the frame is shown only to ask, and hidden after the wait and on one alignment",
              click.Contains("ShowFrame(true);") && click.Contains("ShowFrame(false);   // :347") && click.Contains("ShowFrame(false);   // :351")
              && !click.Contains("elaAlignments.IsVisible = true;"), true);
        Check("Confinement: BUY charges and files the chosen alignment",
              city.Contains("var align = AlignOf(chosenAlign);") && city.Contains("if (!CharPay(ModItemCost(def, align)))") && city.Contains("Alignment = align, IdLevel = 0"), true);
        Check("Store: the row click asks AlignmentChoice, and one alignment is shown without the frame",
              shop.Contains("var offer = AlignmentChoice.For(") && shop.Contains("if (offer.Asks)") && shop.Contains("ShowChosen(row, offer.Chosen(chosenAlign));"), true);
    }
}
