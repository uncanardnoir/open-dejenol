using System.Globalization;
using Mordor.Core;

public static partial class SelfCheckProbe
{
    /// <summary>
    /// Gold is VB Currency (fidelity T3, 2026-10-21). <c>Gold</c> and <c>BankGold</c> are Currency
    /// in the character record (MAIN.TXT:332, 335), as are a chest's <c>Fight.Gold</c> (105) and a
    /// Hall record (846): four decimal places, exact sums, a Double rounded half to even on the way
    /// in and a Long rounded half to even on the way out. The port had kept whole pieces. The one
    /// way a fraction gets into a purse is the Bank's text boxes, <c>CCur</c> of whatever is typed;
    /// from there it travels through every flow that moves gold, and each flow is checked here with
    /// one. The figures on screen are formatNumber's, which has no decimals and rounds a half away
    /// from zero, so a fraction is never seen.
    /// </summary>
    private static void CurrencyChecks(string dir)
    {
        Console.WriteLine("\ngold is Currency: four places kept, rounded where the original rounds (MAIN.TXT:332-335)");
        string Norm(string s) => s.Replace("\r\n", "\n");
        string main = Norm(PortSource("decompiled-named", "MAIN.TXT"));
        string bank = Norm(PortSource("decompiled-named", "FRMCITYB.BAS"));
        string main2 = Norm(PortSource("decompiled-named", "MAINMOD2.BAS"));
        string mons = Norm(PortSource("decompiled-named", "MONSMOD.BAS"));
        string mod1 = Norm(PortSource("decompiled-named", "MODULE1.BAS"));
        string exch = Norm(PortSource("decompiled-named", "FRMEXCHA.BAS"));
        string hall = Norm(PortSource("decompiled-named", "HALL.BAS"));

        // ---- the original ----
        Check("MAIN.TXT: the character's Gold and BankGold are Currency",
              main.Contains("  Gold As Currency\n  Hits As Integer\n  MaxHits As Integer\n  BankGold As Currency\n"), true);
        Check("MAIN.TXT: so is a chest's gold and a Hall record's value",
              main.Contains("  M4F24 As Integer\n  Gold As Currency\n") && main.Contains("Type T73E9\n  M73F5 As Currency\n"), true);
        Check("FRMCITYB.BAS: the Bank's amount is CCur of the text, kept in a Currency",
              bank.Contains("Dim l00E6 As Currency\n    On Error Resume Next\n    If  KeyAscii = 13 Then\n        KeyAscii = 0\n        l00E6 = CCur(txtAmount(Index).Text)\n        If  l00E6 Then"), true);
        Check("FRMCITYB.BAS: the All buttons write LTrim$(Str$(balance))",
              bank.Contains("txtAmount(Index).Text = LTrim$(Str$(Chars(Me).Gold))"), true);
        Check("MAINMOD2.BAS: charPay's charge is a Long, and the bank's part goes back into it",
              main2.Contains("Function charPay (p0196 As Long) As Integer") && main2.Contains("            p0196 = p0196 - Chars(Me).Gold\n            Chars(Me).Gold = 0\n            Chars(Me).BankGold = Chars(Me).BankGold - p0196"), true);
        Check("MONSMOD.BAS: the share is CLng'd, and quoted as formatNumber(CCur(...))",
              mons.Contains("Chars(l039E).Gold = Chars(l039E).Gold + CLng((l039C / (UBound(Chars) + 1)))")
              && mons.Contains("\"Your share is \" + formatNumber(CCur(l039C / (UBound(Chars) + 1)), m03A6) + \" Gold!\""), true);
        Check("MONSMOD.BAS: a thief's take is a Long",
              mons.Contains("Dim l04DE As Long") && mons.Contains("                            l04DE = Chars(pv0442).Gold\n"), true);
        Check("MODULE1.BAS: formatNumber is Format$ with no decimal places",
              mod1.Contains("Function formatNumber (p05FA As Currency, p05FC As Integer) As String") && mod1.Contains("l05FE = Format$(p05FA, \"###,###,###,###,###\")"), true);
        Check("FRMEXCHA.BAS / MAINMOD2.BAS: Exchange and Give take a CLng'd amount",
              exch.Contains("l00A6 = CLng(l00AA)") && main2.Contains("l05DE = CLng(fn170D(\"Give to \""), true);
        Check("HALL.BAS: a record is offered as a Currency",
              hall.Contains("Sub mdrhalCheckRecord (p0100, p0102, ByVal pv0104 As Currency)"), true);

        // ---- the type ----
        string D = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        VbCurrency C(decimal m) => VbCurrency.FromDecimal(m);
        VbCurrency P(string s) => VbCurrency.TryParse(s.Replace(".", D), out var v) ? v : VbCurrency.MinValue;
        Check("ten-thousandths: 10.5 is 105000", C(10.5m).Raw, 105000);
        Check("sums are exact", (C(0.1m) + C(0.2m)).ToString(), "0.3");
        Check("a Double in: 1/3 kept to four places", VbCurrency.FromDouble(1 / 3.0).ToString(), "0.3333");
        Check("a Double in: 2/3 rounds up", VbCurrency.FromDouble(2 / 3.0).ToString(), "0.6667");
        Check("a Double in: Currency * 1.5 through Double", VbCurrency.FromDouble(C(1.25m).ToDouble() * 1.5).ToString(), "1.875");
        Check("a Double in: eight places cut to four", VbCurrency.FromDouble(0.12345678).ToString(), "0.1235");
        Check("CCur of text, half to even at the fourth place", $"{P("1.00005")} {P("1.00015")} {P("-1.00005")}", "1 1.0002 -1");
        Check("CCur of text: exponent, D exponent, hex", $"{P("1E1")} {P("2.5D2")} {P("&H10")}", "10 250 16");
        Check("CCur of text: not a number", VbCurrency.TryParse("ten", out _), false);
        Check("into a Long, half to even", string.Join(" ", new[] { 2.5m, 3.5m, -2.5m, 10.5m, 11.5m, 0.4999m, 0.5001m }.Select(m => C(m).RoundToWhole())), "2 4 -2 10 12 0 1");
        Check("the whole part an older save keeps: toward zero", $"{C(10.9m).Whole} {C(-0.25m).Whole}", "10 0");
        bool Overflows(Func<VbCurrency> f) { try { f(); return false; } catch (OverflowException) { return true; } }
        Check("the top of the range", VbCurrency.MaxValue.ToString(), "922337203685477.5807");
        Check("past it: Overflow (error 6)", $"{Overflows(() => VbCurrency.MaxValue + C(0.0001m))} {Overflows(() => (VbCurrency)922337203685478L)} {Overflows(() => (VbCurrency)922337203685477L)} {Overflows(() => VbCurrency.FromDouble(1e15))}",
              "True True False True");
        Check("Str$: a blank, no leading zero, a '.'", $"[{C(10.5m).Str()}][{C(0.25m).Str()}][{C(-0.25m).Str()}][{C(1500).Str()}]", "[ 10.5][ .25][-.25][ 1500]");

        // ---- how it is shown: formatNumber ----
        string F(VbCurrency v) => CharStatFile.FormatNumber(v);
        Check("formatNumber: no fraction shown, a half rounds away from zero",
              $"{F(C(10.5m))} {F(C(2.5m))} {F(C(10.4999m))} {F(C(-10.5m))}", "11 3 10 -11");
        Check("formatNumber: grouped, as Format$ groups", F(C(1234567.5m)), 1234568L.ToString("###,###,###,###,###", CultureInfo.CurrentCulture));
        Check("formatNumber: nought, and nothing left after rounding", $"[{F(0)}][{F(C(0.4m))}][{F(C(-0.4m))}]", "[0][0][-]");
        Check("formatNumber: whole values as before", F(1500), CharStatFile.FormatNumber(1500L));

        // ---- the Bank: where the fraction comes from ----
        var ch = Character.Create("Banker", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        ch.GoldOnHand = 100; ch.GoldInBank = 0;
        string Bal() => $"{ch.GoldOnHand}/{ch.GoldInBank}";
        Check("deposit 10.5: the half is kept", $"{Party.BankTyped(ch, "10.5".Replace(".", D), true)} {Bal()}", "Moved 89.5/10.5");
        Check("the All box: LTrim$(Str$(89.5))", Party.BankAllText(ch.GoldOnHand), "89.5");
        Check("a balance under one: no leading zero", Party.BankAllText(C(0.25m)), ".25");
        if (D == ".")
            Check("deposit All: the Str$ text read back exactly", $"{Party.BankTyped(ch, Party.BankAllText(ch.GoldOnHand), true)} {Bal()}", "Moved 0/100");
        else { ch.GoldOnHand = 0; ch.GoldInBank = 100; }
        Check("withdraw .25", $"{Party.BankTyped(ch, ".25".Replace(".", D), false)} {Bal()}", "Moved 0.25/99.75");
        Check("an amount that is nought at four places: nothing, the box kept", $"{Party.BankTyped(ch, "0.00004".Replace(".", D), false)} {Bal()}", "Ignored 0.25/99.75");
        Check("not a number: nothing, the box kept", $"{Party.BankTyped(ch, "lots", true)} {Bal()}", "Ignored 0.25/99.75");
        Check("deposit more than on hand: Beep", $"{Party.BankTyped(ch, "0.2501".Replace(".", D), true)} {Bal()}", "Beep 0.25/99.75");
        Check("deposit a negative: Beep", $"{Party.BankTyped(ch, "-5", true)} {Bal()}", "Beep 0.25/99.75");
        Check("withdraw a negative: it pays in, unchecked", $"{Party.BankTyped(ch, "-5", false)} {Bal()}", "Moved -4.75/104.75");
        Check("withdraw from an account below nought: Beep", $"{Party.BankTyped(ch, "1", false)} {(ch.GoldInBank = -1) == -1} {Party.BankTyped(ch, "1", false)}", "Moved True Beep");

        // ---- charPay ----
        ch.GoldOnHand = C(50.5m); ch.GoldInBank = C(49.5m);
        Check("charPay 101 of 100 in all: refused, nothing taken", $"{ch.Pay(101)} {Bal()}", "False 50.5/49.5");
        Check("charPay 100: the bank's 49.5 is a Long, 50, so the account is left at -0.5", $"{ch.Pay(100)} {Bal()}", "True 0/-0.5");
        ch.GoldOnHand = C(10.5m); ch.GoldInBank = 0;
        Check("charPay 10 from 10.5 on hand: the half stays", $"{ch.Pay(10)} {Bal()}", "True 0.5/0");
        Check("charPay 1 from 0.5: refused", $"{ch.Pay(1)} {Bal()}", "False 0.5/0");
        ch.GoldOnHand = C(0.5m); ch.GoldInBank = C(0.5m);
        Check("charPay 1 from 0.5 and 0.5: the bank's 0.5 rounds to 0", $"{ch.Pay(1)} {Bal()}", "True 0/0.5");

        // ---- the thief ----
        Check("a thief's roll under the purse: the roll", CombatEngine.GoldToSteal(C(100.5m), 40), 40);
        Check("a roll past 10.5: the Long takes 10, the half stays", CombatEngine.GoldToSteal(C(10.5m), 99), 10);
        Check("a roll past 11.5: the Long takes 12 (and the -0.5 is put back to 0)", CombatEngine.GoldToSteal(C(11.5m), 99), 12);

        // ---- the share of a fight's gold ----
        Check("5 between two: each paid CLng(2.5) = 2", GoldShare.Share(5, 2), 2);
        Check("... and the line quotes Format$ of 2.5: 3", GoldShare.Line(5, 0, 2), "Your share is 3 Gold!");
        Check("7 between two: paid 4, quoted 4", $"{GoldShare.Share(7, 2)} {GoldShare.Line(7, 0, 2)}", "4 Your share is 4 Gold!");
        Check("10 between three: paid 3, quoted 3 (3.3333)", $"{GoldShare.Share(10, 3)} {GoldShare.Shown(10, 3)} {GoldShare.Line(10, 0, 3)}", "3 3.3333 Your share is 3 Gold!");
        Check("a chest's Currency into the Long, half to even", $"{CombatEngine.GoldAfterClass(2.5m, 2)} {CombatEngine.GoldAfterClass(3.5m, 2)} {CombatEngine.GoldAfterClass(5m, 0)}", "2 4 2");

        // ---- Give and the Exchange ----
        var other = Character.Create("Other", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        ch.GoldOnHand = C(10.5m); ch.GoldInBank = 5; other.GoldOnHand = 0; other.GoldInBank = 0;
        Check("Give: \"10.5\" is no more than 10.5 on hand (Val against the Currency)", GiveCommand.GoldAnswer("10.5", ch.GoldOnHand).Refusal, null);
        Check("Give: 11 is more", GiveCommand.GoldAnswer("11", ch.GoldOnHand).Refusal, GiveCommand.NotEnoughGold);
        Check("Give: the amount is CLng'd, 10.5 to 10", GiveCommand.GoldFromTag("10.5"), 10);
        GiveCommand.Give(ch, other, 0, 10, _ => null);
        Check("Give: 10 moved, the half stays", $"{ch.GoldOnHand} {other.GoldOnHand}", "0.5 10");
        ch.GoldOnHand = C(10.5m); other.GoldOnHand = 0;
        Check("Exchange in town: 12 from 10.5 on hand and 5 banked", $"{Party.TransferGold(ch, other, 12)} {Bal()} {other.GoldOnHand}", " 0/3.5 12");
        ch.DungeonLevel = 1; ch.GoldOnHand = C(10.5m); ch.GoldInBank = 5;
        Check("Exchange below: 11 is too much for 10.5 on hand", Party.TransferGold(ch, other, 11), "Too Much!");
        ch.DungeonLevel = 0;

        // ---- the party's options ----
        ch.GoldOnHand = C(1.25m); ch.GoldInBank = C(0.5m); other.GoldOnHand = C(2.5m); other.GoldInBank = C(0.75m);
        Check("Party Deposit: each exactly", $"{Party.PartyDeposit(new[] { ch, other })} {Bal()} {other.GoldOnHand}/{other.GoldInBank}", "3.75 0/1.75 0/3.25");
        ch.GoldOnHand = C(1.25m); other.GoldOnHand = C(2.5m);
        Party.PoolAndDeposit(new[] { ch, other }, ch);
        Check("Pool & Deposit: every last bit to the one in command", $"{Bal()} {other.GoldOnHand}/{other.GoldInBank}", "0/8.75 0/0");
        var pool = new Party(ch, other);
        ch.GoldOnHand = 0; other.GoldOnHand = C(0.0001m);
        Check("Alt+P: a ten-thousandth pooled", $"{pool.PoolGold()} {ch.GoldOnHand}", "0.0001 0.0001");

        // ---- the Hall: Wealthiest ----
        var h = HallRecords.Fresh();
        h.Records[(int)HallRecords.Kind.Wealthiest] = new HallRecords.Record { Value = 1000, Holder = "Someone" };
        Check("1,000.5 in the bank beats a record of 1,000", h.Check(HallRecords.Kind.Wealthiest, ch, C(1000.5m)) != null, true);
        Check("... and the record keeps the half", h.For(HallRecords.Kind.Wealthiest).Value.ToString(), "1000.5");
        Check("1,000.5 again does not", h.Check(HallRecords.Kind.Wealthiest, other, C(1000.5m)), null);
        string hj = System.Text.Json.JsonSerializer.Serialize(h);
        var h2 = System.Text.Json.JsonSerializer.Deserialize<HallRecords>(hj)!;
        Check("the Hall's file: the whole value where it was, and the exact beside it",
              hj.Contains("\"Value\":1000,") && hj.Contains("\"ValueExact\":1000.5"), true);
        Check("... read back exactly", h2.For(HallRecords.Kind.Wealthiest).Value.ToString(), "1000.5");
        Check("a whole record writes no exact figure", System.Text.Json.JsonSerializer.Serialize(HallRecords.Fresh()).Contains("ValueExact"), false);

        // ---- the save ----
        string tmp = Path.Combine(Path.GetTempPath(), $"mordor-currency-{Environment.ProcessId}.json");
        try
        {
            var s = Character.Create("Saver", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
            s.GoldOnHand = C(10.5m); s.GoldInBank = C(-0.25m);
            s.Save(tmp);
            string json = File.ReadAllText(tmp);
            Check("saved: the integer fields an older build reads",
                  json.Contains("\"GoldOnHand\": 10,") && json.Contains("\"GoldInBank\": 0,"), true);
            Check("saved: the exact figures beside them", json.Contains("\"GoldOnHandExact\": 10.5") && json.Contains("\"GoldInBankExact\": -0.25"), true);
            var back = Character.Load(tmp);
            Check("loaded: exactly what was saved", $"{back.GoldOnHand}/{back.GoldInBank}", "10.5/-0.25");

            // an older build reads the file: it knows only the integers, and skips the rest
            var old = System.Text.Json.JsonSerializer.Deserialize<OlderSave>(json)!;
            Check("an older build reading it: the whole parts, no error", $"{old.GoldOnHand}/{old.GoldInBank}", "10/0");

            // a save from before: no exact field at all
            s.GoldOnHand = 1500; s.GoldInBank = 2312;
            s.Save(tmp);
            json = File.ReadAllText(tmp);
            Check("whole gold writes no exact figure (the file as it always was)", json.Contains("Exact"), false);
            Check("an old save loads unchanged", $"{Character.Load(tmp).GoldOnHand}/{Character.Load(tmp).GoldInBank}", "1500/2312");

            // an exact figure that no longer agrees with the integer (the integer edited by hand): the integer wins
            File.WriteAllText(tmp, json.Replace("\"GoldOnHand\": 1500,", "\"GoldOnHand\": 12, \"GoldOnHandExact\": 10.5,"));
            Check("an exact figure that disagrees with the integer is not believed", Character.Load(tmp).GoldOnHand.ToString(), "12");
            File.WriteAllText(tmp, json.Replace("\"GoldOnHand\": 1500,", "\"GoldOnHandExact\": 12.75, \"GoldOnHand\": 12,"));
            Check("... and one that agrees is, in either order", Character.Load(tmp).GoldOnHand.ToString(), "12.75");

            // ---- a save past the range never costs the file ----
            File.WriteAllText(tmp, json.Replace("\"GoldOnHand\": 1500,", "\"GoldOnHand\": 99999999999999999, \"GoldOnHandExact\": 1e40,")
                                       .Replace("\"GoldInBank\": 2312,", "\"GoldInBank\": -1e30,"));
            var big = Character.Load(tmp);
            Check("a whole part past Currency, past a long, an exact past a decimal: held to the range, the file loads",
                  $"{big.Name} {big.GoldOnHand == VbCurrency.Saturate(long.MaxValue)} {big.GoldInBank == VbCurrency.Saturate(long.MinValue)}", "Saver True True");
        }
        finally { try { File.Delete(tmp); } catch { } }

        string hallDir = Path.Combine(Path.GetTempPath(), $"mordor-currency-hall-{Environment.ProcessId}");
        try
        {
            Directory.CreateDirectory(hallDir);
            var rec = HallRecords.Fresh();
            rec.Records[(int)HallRecords.Kind.Wealthiest] = new HallRecords.Record { Value = 7, Holder = "Kept" };
            rec.Save(hallDir);
            string hp = Path.Combine(hallDir, "records.json");
            File.WriteAllText(hp, File.ReadAllText(hp).Replace("\"Value\": 7,", "\"Value\": 1e20, \"ValueExact\": \"junk\","));
            var read = HallRecords.Load(hallDir);
            Check("a Hall record past the range: the file is read, not replaced by a fresh Hall",
                  $"{read.For(HallRecords.Kind.Wealthiest).Holder} {read.For(HallRecords.Kind.Wealthiest).Value == VbCurrency.Saturate(long.MaxValue)}", "Kept True");
        }
        finally { try { Directory.Delete(hallDir, true); } catch { } }

        CurrencyOverflowChecks();
    }

    /// <summary>
    /// Overflow never ends the port (review of 511d570). Each call site does what its original's
    /// error handling does: On Error Resume Next skips the one statement, a trap leaves the
    /// routine where it got to, and where there is no handler (the original program would end
    /// before saving) the port changes nothing.
    /// </summary>
    private static void CurrencyOverflowChecks()
    {
        Console.WriteLine("\ngold at the edge of Currency's range: never a crash, the original's error handling per call site");
        VbCurrency C(decimal m) => VbCurrency.FromDecimal(m);
        var top = VbCurrency.MaxValue - C(10);
        bool Threw(Action a) { try { a(); return false; } catch (OverflowException) { return true; } }

        Check("TryAdd / TrySubtract at the edges",
              $"{VbCurrency.TryAdd(VbCurrency.MaxValue, C(0.0001m), out _)} {VbCurrency.TryAdd(top, C(10), out _)} {VbCurrency.TrySubtract(VbCurrency.MinValue, C(0.0001m), out _)} {VbCurrency.TrySubtract(C(0), VbCurrency.MinValue, out _)}",
              "False True False False");
        Check("saturating sums", $"{VbCurrency.AddSaturating(top, top) == VbCurrency.MaxValue} {VbCurrency.SubtractSaturating(VbCurrency.MinValue, C(1)) == VbCurrency.MinValue}", "True True");
        Check("TryFromDouble past the range", VbCurrency.TryFromDouble(1e300, out _), false);

        var ch = Character.Create("Edge", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        string Bal(Character c) => $"{c.GoldOnHand}/{c.GoldInBank}";

        // the Bank's box: On Error Resume Next, statement by statement
        ch.GoldOnHand = 0; ch.GoldInBank = top;
        bool threw = Threw(() => Party.BankTyped(ch, "-100", false));
        Check("a negative withdrawal past the top: no crash; the account skipped, the hand still debited (Resume Next)",
              $"{threw} {Bal(ch)}", $"False -100/{top}");
        ch.GoldOnHand = 100; ch.GoldInBank = VbCurrency.MaxValue;
        Check("a deposit into a full account: the account skipped, the hand debited",
              $"{Party.BankTyped(ch, "50", true)} {Bal(ch)}", $"Moved 50/{VbCurrency.MaxValue}");

        // Purse and charPay
        ch.GoldOnHand = VbCurrency.MaxValue; ch.GoldInBank = VbCurrency.MaxValue;
        Check("a purse past the range is held to it, and charPay still works", $"{ch.Purse == VbCurrency.MaxValue} {ch.Pay(10)}", "True True");
        Check("the shown total", CharStatFile.FormatNumber(ch.Purse), CharStatFile.FormatNumber(VbCurrency.MaxValue));

        // the Bank's party options: no handler, so all or nothing
        var a = Character.Create("A", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        var b = Character.Create("B", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        a.GoldOnHand = 5; a.GoldInBank = 0; b.GoldOnHand = 100; b.GoldInBank = top;
        Check("Party Deposit that would overflow one account: nothing moves, not even the other's",
              $"{Party.PartyDeposit(new[] { a, b })} {Bal(a)} {Bal(b)}", $"0 5/0 100/{top}");
        a.GoldOnHand = 5; a.GoldInBank = top; b.GoldOnHand = 100; b.GoldInBank = 0;
        Check("Pool & Deposit that would overflow: nothing moves", $"{Party.PoolAndDeposit(new[] { a, b }, a)} {Bal(a)} {Bal(b)}", $"False 5/{top} 100/0");
        Check("Party Pool that would overflow: nothing moves", $"{Party.PoolToBank(new[] { b, a }, a)} {Bal(a)} {Bal(b)}", $"False 5/{top} 100/0");
        a.GoldInBank = 0;
        Check("... and with room, they do", $"{Party.PoolToBank(new[] { a, b }, a)} {Bal(a)} {Bal(b)}", "True 0/105 0/0");

        // Alt+P: fn1521's trap, so the members before the overflow are pooled
        var c3 = Character.Create("C", RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 15, 15, 15, 15, 15, 15 });
        ch.GoldOnHand = top; ch.GoldInBank = 0; a.GoldOnHand = 10; b.GoldOnHand = 100; c3.GoldOnHand = 1;
        var pool = new Party(ch, a, b, c3);
        Check("Alt+P past the top: the first pooled, the one that overflows keeps theirs, the rest not asked",
              $"{pool.PoolGold()} {ch.GoldOnHand == VbCurrency.MaxValue} {a.GoldOnHand} {b.GoldOnHand} {c3.GoldOnHand}", "10 True 0 100 1");

        // Exchange: the trap leaves where it got to
        a.DungeonLevel = 0; a.GoldOnHand = VbCurrency.MaxValue; a.GoldInBank = 1;
        Check("Exchange from a purse whose halves overflow together: nothing moves", $"{Party.TransferGold(a, b, 5)} {b.GoldOnHand}", " 100");
        a.GoldInBank = 0; b.GoldOnHand = VbCurrency.MaxValue;
        Check("Exchange onto a full hand: nothing moves", $"{Party.TransferGold(a, b, 5)} {a.GoldOnHand == VbCurrency.MaxValue}", " True");

        // Give: fn1521's trap, nothing said
        a.GoldOnHand = 50; b.GoldOnHand = VbCurrency.MaxValue;
        var said = GiveCommand.Give(a, b, 0, 10, _ => null);
        Check("Give onto a full hand: nothing moved, no line", $"{said.Count} {a.GoldOnHand}", "0 50");

        // a thief: Gold - l04DE overflowing ends the group's attack
        Check("a thief's subtraction past the bottom: null (the trap ends the attack)", CombatEngine.GoldToSteal(VbCurrency.MinValue, 40) == null, true);

        // a fight's share: monGetGold's trap, the members before are paid
        a.GoldOnHand = 0; b.GoldOnHand = top;
        var fight = new CombatEngine(new Party(a, b), new List<CombatMonster>(), _ => null);
        fight.GoldRecovered[a] = 40;
        bool shareThrew = Threw(fight.PayGold);
        Check("a share onto a full hand: no crash; the first paid, no line",
              $"{shareThrew} {a.GoldOnHand} {b.GoldOnHand == top} {fight.GoldLine == null}", "False 20 True True");

        Check("fn0D4D's handler line", EncounterGenerator.GoldOverflowLine, "Base Error 'Overflow' Generating Monsters!");
    }

    /// <summary>The character record as a build from before Currency gold reads it: the two integers only.</summary>
    private sealed class OlderSave
    {
        public long GoldOnHand { get; set; }
        public long GoldInBank { get; set; }
    }
}
