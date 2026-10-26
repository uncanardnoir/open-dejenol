using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Mordor.Core;
using Mordor.Core.Import;

/// <summary>
/// Importing the original game's saves (Mordor.Core.Import, docs/GAME_FILES.md "Importing the
/// original's saves"), in four parts:
/// <list type="number">
/// <item>the record layouts against offsets counted by hand from MAIN.TXT's declarations, among them
/// GuildsJoined at 1678 in a character record, the offset seen when editing a copy of MDATA4;</item>
/// <item>a DATA folder built byte by byte at those offsets, read back and written into a scratch
/// saves folder, every field and file checked - this runs everywhere, the public tree too;</item>
/// <item>every declared type against MAIN.TXT itself and every file's Len against each
/// <c>Open ... For Random ... Len =</c> in the modules (the decompiled source);</item>
/// <item>a real DATA folder, when MORDOR_ORIGINAL_DATA names one: sizes, an import end to end,
/// value plausibility, each character loaded and saved again unchanged. Skipped, not failed,
/// without one; the original's data is never in the tree.</item>
/// </list>
/// </summary>
public static partial class SelfCheckProbe
{
    // T5810's offsets, counted by hand from MAIN.TXT (Integer 2, Long and Single 4, Currency 8,
    // String * n its length; Pack and M5860 41 slots of 18 bytes, GuildsJoined 16 of 24,
    // Companions 5 of 33, M5958 21 of 18): independent of VbType's own arithmetic
    private static readonly (string Field, int At)[] HandOffsets =
    {
        ("Name", 0), ("RaceIndex", 30), ("Alignment", 32), ("M5829", 34), ("Age", 36), ("SpellPoints", 40),
        ("DungeonLevel", 42), ("X", 44), ("Y", 46), ("Att", 48), ("Def", 52), ("Stats", 56), ("StatMods", 70),
        ("Pack", 84), ("M5860", 822), ("M586D", 1560), ("Facing", 1642), ("M579B", 1646), ("Gold", 1654),
        ("Hits", 1662), ("MaxHits", 1664), ("BankGold", 1666), ("M56AF", 1674), ("CurrentGuild", 1676),
        ("GuildsJoined", 1678), ("Companions", 2062), ("M5700", 2227), ("M58DC", 2229), ("M58E9", 2231),
        ("M58F9", 2233), ("M5906", 2263), ("M5914", 2335), ("Ailments", 2347), ("Resistances", 2363),
        ("SpellEffects", 2387), ("SpellResistances", 2391), ("DeathState", 2415), ("M5943", 2417),
        ("M594C", 2419), ("M5958", 2429), ("M5962", 2807), ("M5970", 2809), ("ItemEffects", 2813),
        ("ItemResistances", 2817), ("M5997", 2841), ("M59A1", 2845), ("M59B0", 2847), ("M59BB", 2869),
        ("M59CB", 2871), ("M59D9", 2873), ("Sanctuary", 2875), ("M59F4", 2881),
    };

    private static void ImportChecks(string dir)
    {
        Console.WriteLine("\nimporting the original's saves: the record layouts (Mordor.Core.Import.VbLayout)");
        var ch = VbLayout.Character;
        Check("T5810: GuildsJoined at record offset 1678 (as seen editing a copy of MDATA4)", ch.OffsetOf("GuildsJoined"), 1678);
        var wrong = HandOffsets.Where(h => ch.OffsetOf(h.Field) != h.At).Select(h => $"{h.Field} {ch.OffsetOf(h.Field)} not {h.At}").ToList();
        Check($"T5810: every field where MAIN.TXT's declarations put it ({HandOffsets.Length} counted by hand)", wrong.Count == 0 ? "all" : string.Join(", ", wrong), "all");
        Check("T5810 is 2887 bytes, inside MDATA4's Len of 2900", $"{ch.Size} {VbLayout.CharactersLen}", "2887 2900");
        Check("... GuildsJoined(3).Level is three 24-byte guilds on, Pack(7).Id seven 18-byte slots and 6",
              $"{ch.OffsetOf("GuildsJoined(3).Level")} {ch.OffsetOf("Pack(7).Id")} {ch.OffsetOf("Companions(2).Ref")}", $"{1678 + 72} {84 + 7 * 18 + 6} {2062 + 66 + 15}");
        Check("the other types' sizes: T576E T578A T57BF T57EF T4E6B T5755 T4E8B T4F0B T5BF1 T73E9",
              string.Join(" ", new[] { VbLayout.Slot, VbLayout.Guild, VbLayout.Companion, VbLayout.Window, VbLayout.Sanctuary,
                                       VbLayout.Shelf, VbLayout.Foe, VbLayout.Fight, VbLayout.Library, VbLayout.Hall }.Select(t => t.Size)),
              "18 24 33 18 6 14 26 16 58 42");
        Check("each type fits the Len its file is opened with",
              VbLayout.Shelf.Size <= VbLayout.StoreLen && VbLayout.Shelf.Size <= VbLayout.PensLen && VbLayout.Foe.Size <= VbLayout.RoomsLen
              && VbLayout.Fight.Size <= VbLayout.RoomsLen && VbLayout.Library.Size <= VbLayout.LibraryLen && VbLayout.Hall.Size <= VbLayout.HallLen, true);
        Check("fn0326: \"1.1\" and \"\" are accepted, \"1.0\" and \"1.10\" refused",
              $"{VbLayout.VersionAccepted("1.1")} {VbLayout.VersionAccepted("")} {VbLayout.VersionAccepted("1.0")} {VbLayout.VersionAccepted("1.10")}", "True True False False");

        SyntheticImportChecks(dir);
        Section("importing: the layouts against MAIN.TXT and the modules' Open ... Len", ImportLayoutAgainstSource);
        RealImportChecks(dir);
    }

    // ---- byte writers for the synthetic files

    private static void P16(byte[] b, int at, int v) => BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(at), (short)v);
    private static void P32(byte[] b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v);
    private static void PF32(byte[] b, int at, float v) => BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at), v);
    private static void P64(byte[] b, int at, long v) => BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(at), v);
    private static void PStr(byte[] b, int at, string s, int len)
    {
        for (int i = 0; i < len; i++) b[at + i] = i < s.Length ? (byte)s[i] : (byte)' ';
    }
    private static byte[] VarStr(string s)
    {
        var b = new byte[2 + s.Length];
        P16(b, 0, s.Length);
        Encoding.Latin1.GetBytes(s).CopyTo(b, 2);
        return b;
    }
    private static byte[] Int16Record(int v) { var b = new byte[2]; P16(b, 0, v); return b; }

    /// <summary>A random-access file as VB3 leaves it: each record at (n - 1) * Len, the last one as long as what was put there.</summary>
    private static void WriteRandom(string path, int len, IList<byte[]> records)
    {
        using var s = File.Create(path);
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var rec = new byte[i == records.Count - 1 ? r.Length : len];
            Array.Copy(r, rec, Math.Min(r.Length, rec.Length));
            s.Write(rec);
        }
    }

    private static void SyntheticImportChecks(string dir)
    {
        Console.WriteLine("\nimporting: a DATA folder built byte by byte at those offsets, read back and written into a scratch saves folder");
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        string root = Path.Combine(Path.GetTempPath(), "mordor-selfcheck-import-" + Environment.ProcessId);
        string data = Path.Combine(root, "DATA"), saves = Path.Combine(root, "saves");
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(data);
            int item = items.FindIndex(i => i.Id > 0 && i.Floor > 0);      // a table index past 0
            int mon = Math.Min(5, monsters.Count - 1);
            int guildB = Math.Min(2, GuildData.All.Count - 1);

            // ---- MDATA4: version, count 2, a deleted record, "Synth", "Second", and "Ghost" past the count (short, the last)
            var a = new byte[VbLayout.CharactersLen];
            PStr(a, 0, "Synth", 30); P16(a, 30, 1); P16(a, 32, 2); P16(a, 34, 1); PF32(a, 36, 6000f); P16(a, 40, 20);
            P16(a, 42, 3); P16(a, 44, 5); P16(a, 46, 7); PF32(a, 48, 0f); PF32(a, 52, 0f);
            for (int i = 0; i < 6; i++) P16(a, 56 + i * 2, 10 + i);
            int slot1 = 84 + 18;                       // Pack(1): Att Def Ref Id Alignment Charges Equipped IdLevel Cursed
            P16(a, slot1 + 4, item); P16(a, slot1 + 6, items[item].Id); P16(a, slot1 + 8, 2); P16(a, slot1 + 10, 4);
            P16(a, slot1 + 12, 1); P16(a, slot1 + 14, 1); P16(a, slot1 + 16, 1);
            int bank2 = 822 + 2 * 18;                  // M5860(2), after two empty bank slots
            P16(a, bank2 + 4, item); P16(a, bank2 + 6, items[item].Id);
            P16(a, 1642, 2); P64(a, 1646, 555 * 10000L); P64(a, 1654, 12345678); P16(a, 1662, 30); P16(a, 1664, 40);
            P64(a, 1666, 1000 * 10000L); P16(a, 1676, guildB);
            P16(a, 1678, 5); P64(a, 1678 + 2, 100 * 10000L); P16(a, 1678 + 10, 1); P16(a, 1678 + 12, 77);   // Nomad 5, a quest
            P16(a, 1678 + guildB * 24, 3); P64(a, 1678 + guildB * 24 + 2, 7 * 10000L);                     // the current guild, its slot's copy stale
            for (int s = 0; s < 5; s++) P16(a, 2062 + s * 33 + 15, -1);
            int comp1 = 2062 + 33;                     // Companions(1): Name Ref M57D4 Hits MaxHits Alignment Att Def BindingLevel IdLevel
            PStr(a, comp1, "Rex", 15); P16(a, comp1 + 15, mon); P16(a, comp1 + 17, monsters[mon].Id); P16(a, comp1 + 19, 9);
            P16(a, comp1 + 21, 12); P16(a, comp1 + 23, 2); P16(a, comp1 + 25, monsters[mon].Att); P16(a, comp1 + 27, monsters[mon].Def);
            P16(a, comp1 + 29, 7); P16(a, comp1 + 31, 1);
            P16(a, 2229, 21); P16(a, 2231, 55); PStr(a, 2233, "Healer", 30);
            P64(a, 2263, 42 * 10000L); P64(a, 2263 + 8, 3 * 10000L); P64(a, 2263 + 40, 7200 * 10000L);
            P16(a, 2335, -1); P16(a, 2335 + 6, -1); P16(a, 2335 + 10, 4);
            P16(a, 2347, 4); P16(a, 2347 + 6, 1);
            P32(a, 2387, 0x10); P16(a, 2391 + 2 * 3, 25);
            P16(a, 2417, 1); PStr(a, 2419, "pw", 10);
            P32(a, 2429 + 13 * 18, 1500); P32(a, 2429 + 13 * 18 + 4, 300); P32(a, 2429 + 13 * 18 + 8, 7320); P32(a, 2429 + 13 * 18 + 12, 3450); P16(a, 2429 + 13 * 18 + 16, 2);
            P16(a, 2807, 4);
            for (int i = 0; i < 11; i++) P16(a, 2847 + i * 2, i == 0 ? 41 : -1);
            P16(a, 2869, 12); P16(a, 2875, 3); P16(a, 2877, 4); P16(a, 2879, 2); P16(a, 2883, -1);
            // its stored Att/Def: what the port works out, 3 more (a paralysis's left-over shift)
            var b = new byte[VbLayout.CharactersLen];
            PStr(b, 0, "Second", 30); PF32(b, 36, 5840f); P16(b, 44, 9); P16(b, 46, 11); P16(b, 1642, 1);
            for (int i = 0; i < 6; i++) P16(b, 56 + i * 2, 12);
            P16(b, 1662, 15); P16(b, 1664, 15); P16(b, 1678, 1); P16(b, 2417, -1);
            for (int s = 0; s < 5; s++) P16(b, 2062 + s * 33 + 15, -1);
            PF32(b, 48, Rules.BaseAD + 3 + Rules.StatsAD(new[] { 12, 12, 12, 12, 12, 12 }) + Rules.GuildAD(GuildData.All[0].Name, 1).att);
            PF32(b, 52, Rules.BaseAD + 3 + Rules.StatsAD(new[] { 12, 12, 12, 12, 12, 12 }) + Rules.GuildAD(GuildData.All[0].Name, 1).def);
            var ghost = new byte[VbLayout.Character.Size];
            PStr(ghost, 0, "Ghost", 30); P16(ghost, 1678, 1);
            WriteRandom(Path.Combine(data, "MDATA4.MDR"), VbLayout.CharactersLen,
                        new[] { VarStr("1.1"), Int16Record(2), new byte[VbLayout.CharactersLen], a, b, ghost });

            // ---- MDATA6: one shelf, three Neutral of the item
            var shelves = new List<byte[]> { VarStr("1.1"), Int16Record(55), Int16Record(items.Count) };
            for (int i = 0; i < items.Count; i++)
            {
                var r = new byte[VbLayout.Shelf.Size];
                if (i == item) { P16(r, 0, i); P16(r, 2, items[i].Id); P16(r, 4 + 2 * 2, 3); }
                shelves.Add(r);
            }
            WriteRandom(Path.Combine(data, "MDATA6.MDR"), VbLayout.StoreLen, shelves);
            // ---- MDATA15: two of the creature, Neutral (Align(1))
            var pens = new List<byte[]> { VarStr("1.1"), Int16Record(7), Int16Record(monsters.Count) };
            for (int i = 0; i < monsters.Count; i++)
            {
                var r = new byte[VbLayout.Shelf.Size];
                if (i == mon) { P16(r, 0, i); P16(r, 2, monsters[i].Id); P16(r, 4 + 2, 2); }
                pens.Add(r);
            }
            WriteRandom(Path.Combine(data, "MDATA15.MDR"), VbLayout.PensLen, pens);
            // ---- MDATA7: the first guild's master
            var gms = new List<byte[]> { VarStr("1.1") };
            for (int g = 0; g < GuildData.All.Count; g++)
            {
                var name = VarStr(g == 0 ? "Boss" : "");
                var r = new byte[name.Length + 2];
                name.CopyTo(r, 0);
                P16(r, name.Length, g == 0 ? 70 : 0);
                gms.Add(r);
            }
            WriteRandom(Path.Combine(data, "MDATA7.MDR"), VbLayout.GuildmastersLen, gms);
            // ---- MDATA8: deepest 4; level 2's square 3,4 stood on, with an east wall
            var map = new List<byte[]> { VarStr("1.1"), Int16Record(4) };
            for (int i = 1; i <= 15 * 900; i++)
            {
                var r = new byte[4];
                if (i == 900 + 3 + 30 * (4 - 1)) P32(r, 0, (1 << 23) | (1 << Floor.WALL_EAST));
                map.Add(r);
            }
            WriteRandom(Path.Combine(data, "MDATA8.MDR"), VbLayout.AutomapLen, map);
            // ---- MDATA9: two entries
            var log = new MemoryStream();
            void LogEntry(int guild, int day, string text)
            {
                var e = new byte[8 + text.Length];
                P16(e, 0, guild); P32(e, 2, day); P16(e, 6, text.Length); Encoding.Latin1.GetBytes(text).CopyTo(e, 8);
                log.Write(e);
            }
            log.Write(Int16Record(2));
            LogEntry(1, 46000, "\"Synth\" joined our guild.");
            LogEntry(0, 46001, "\"Second\" died on level 1.");
            File.WriteAllBytes(Path.Combine(data, "MDATA9.MDR"), log.ToArray());
            // ---- MDATA10: 15 levels, level 1's tally 77, level 2 area 3 with a standing group and a chest
            var rooms = new List<byte[]> { Int16Record(15) };
            for (int l = 1; l <= 15; l++) rooms.Add(Int16Record(l == 1 ? 77 : 100));
            for (int i = 0; i < 15 * 1000; i++) rooms.Add(new byte[VbLayout.RoomsLen]);
            int roomAt = (3 - 1) * 5 + 17 + (2 - 1) * 1000;   // record number, from 1
            var foe0 = rooms[roomAt - 1];
            P16(foe0, 12, mon); P16(foe0, 14, 2); PF32(foe0, 16, 3600.5f); P16(foe0, 10, 1);
            var fight = rooms[roomAt + 4 - 1];
            P16(fight, 0, 2); P16(fight, 2, mon); P64(fight, 4, 50000); P16(fight, 14, 3);
            rooms[^1] = rooms[^1].Take(VbLayout.Fight.Size).ToArray();
            WriteRandom(Path.Combine(data, "MDATA10.MDR"), VbLayout.RoomsLen, rooms);
            // ---- MDATA12: Synth's party
            WriteRandom(Path.Combine(data, "MDATA12.MDR"), VbLayout.PartiesLen,
                        new[] { Int16Record(1), VarStr("Synth"), VarStr("Second"), VarStr(""), VarStr("") });
            // ---- MDATA13: the creature known, seen 3 times
            var lib = new List<byte[]> { VarStr("1.1"), Int16Record(monsters.Count), Int16Record(items.Count), Int16Record(1), Int16Record(0) };
            for (int i = 0; i < monsters.Count + items.Count; i++)
            {
                var r = new byte[VbLayout.Library.Size];
                if (i == mon) { P16(r, 0, monsters[i].Id); P16(r, 2, 4); P64(r, 4, 3 * 10000L); PStr(r, 12, "Synth", 30); P16(r, 42, -1); P32(r, 44, 46000); PStr(r, 48, "2", 8); P16(r, 56, 1); }
                lib.Add(r);
            }
            WriteRandom(Path.Combine(data, "MDATA13.MDR"), VbLayout.LibraryLen, lib);
            // ---- MDATA14: twelve records and "Nothing", the first over where a stamp would be
            var hall = new List<byte[]>();
            for (int k = 0; k <= 12; k++)
            {
                var r = new byte[VbLayout.Hall.Size];
                P64(r, 0, (k == 0 ? 25 : k) * 10000L); PStr(r, 8, k == 0 ? "Synth" : "Someone", 30); P32(r, 38, k == 0 ? 46000 : 0);
                hall.Add(r);
            }
            WriteRandom(Path.Combine(data, "MDATA14.MDR"), VbLayout.HallLen, hall);

            // ---- read it
            var src = OriginalSaves.Read(data, items, monsters);
            Check("the folder found from its parent too (BasePath + DATA\\)", OriginalSaves.FindDataDir(root), data);
            Check("MDATA4: the count's two characters, the deleted record passed over, Ghost past the count left out",
                  string.Join(",", src.Characters.Select(c => c.Name)), "Synth,Second");
            Check("... and Ghost noted as left out", src.Notes.Any(n => n.Text.Contains("\"Ghost\" past the count")), true);
            var s1 = src.Characters[0].Character!;
            Check("Synth: race, alignment, sex, age, stats",
                  $"{s1.Race} {s1.Alignment} {s1.Sex} {s1.Age} {string.Join(" ", s1.Stats)}",
                  $"{RaceData.All[1].Name} Evil Female 6000 10 11 12 13 14 15");
            Check("Synth: level 3 at 5,7 (0-based 4,6) facing 2, area 12, lost position only, sanctuary 3,4,2",
                  $"{s1.DungeonLevel} {s1.X} {s1.Y} {s1.Facing} {s1.Area} {s1.LostLevel} {s1.LostPosition} {s1.LostFacing} {s1.SanctuaryX},{s1.SanctuaryY},{s1.SanctuaryLevel}",
                  "3 4 6 2 12 False True False 3,4,2");
            Check("Synth: Currency gold to the ten-thousandth, the bank, hits, spell points",
                  $"{s1.GoldOnHand} {s1.GoldInBank} {s1.CurHits}/{s1.MaxHits} {s1.CurSpellPoints}", "1234.5678 1000 30/40 20");
            Check("Synth: the current guild first, its experience from M579B (not its stale slot), then Nomad with its quest",
                  string.Join("; ", s1.Guilds.Select(g => $"{g.Guild} {g.Level} {g.Experience} {g.QuestType}/{g.QuestTarget}")),
                  guildB == 0 ? $"{GuildData.All[0].Name} 3 555 0/0" : $"{GuildData.All[guildB].Name} 3 555 0/0; {GuildData.All[0].Name} 5 100 1/77");
            var ps = s1.Pack[1];
            Check("Synth: pack slot 2 by the table's Id, Neutral, 4 charges, equipped, ID 1 = Mostly, cursed",
                  ps == null ? "empty" : $"{ps.ItemId} {ps.Alignment} {ps.Charges} {ps.Equipped} {ps.Known} {ps.SlotCursed}",
                  $"{items[item].Id} Neutral 4 True Mostly True");
            Check("Synth: the bank keeps its two holes before the item, and nothing after",
                  $"{s1.Bank.Count} {s1.Bank[0].ItemId} {s1.Bank[1].ItemId} {s1.Bank[2].ItemId}", $"3 0 0 {items[item].Id}");
            var rex = s1.Companions[1];
            Check("Synth: companion 2 by Ref and Id, Evil (2 + 1), 9/12, bound 7, ID 1; carrying it",
                  rex == null ? "none" : $"{rex.Name} {rex.Ref} {rex.MonsterId} {rex.Alignment} {rex.Hits}/{rex.MaxHits} {rex.BindingLevel} {rex.IdLevel} {s1.CarryingCompanion}",
                  $"Rex {mon} {monsters[mon].Id} Evil 9/12 7 1 1");
            Check("Synth: companions 1, 3 and 4 empty", $"{s1.Companions[0] == null} {s1.Companions[2] == null} {s1.Companions[3] == null}", "True True True");
            Check("Synth: poison 4, levitating, spell effects &H10, spell resistance 3 at 25",
                  $"{s1.Poisoned} {s1.PoisonLevel} {s1.Levitating} {s1.SpellEffects} {s1.SpellResist[3]}", "True 4 True 16 25");
            Check("Synth: a raise waiting (kind 21, 55%, by Healer), 42 kills, 3 deaths, 2 hours played",
                  $"{s1.RaiseKind} {s1.RaiseChance} {s1.RaisedBy} {s1.Kills} {s1.Deaths} {s1.PlaySeconds}", "21 55 Healer 42 3 7200");
            Check("Synth: options Auto Change Level and Preload, Combat Setting 4, password, buffer 1 a spell",
                  $"{s1.Options.AutoChangeLevel} {s1.Options.Hide} {s1.Options.PreloadCharacter} {s1.Options.NoPerceptionMessages} {s1.CombatSetting} {s1.Password} {s1.Buffers[0]} {s1.Buffers[1]}",
                  "True False True False 4 pw 41 -1");
            var dw = s1.Windows[(int)GameWindow.Dungeon];
            Check("Synth: the Dungeon window's twips as pixels (15 to the pixel), maximised",
                  $"{dw.Left} {dw.Top} {dw.Width} {dw.Height} {dw.State}", "100 20 488 230 2");
            var s2 = src.Characters[1].Character!;
            Check("Second: Att/Def 3 past what the port works out kept as ADShift 3", s2.ADShift, 3);
            Check("Second: no password is the lone space", s2.Password, Character.NoPassword);
            Check("nothing refused", string.Join(" | ", src.Notes.Where(n => n.Kind == NoteKind.Refused)), "");

            Check("MDATA6: the shelf by its item Id and alignment", src.Store == null ? "none" : string.Join(",", src.Store.Shelf.Select(k => $"{k.Key}={k.Value}")), $"{items[item].Id}:2=3");
            Check("MDATA15: the pen by table index, Align(1) as the port's 2 (Neutral)", src.Pens == null ? "none" : string.Join(",", src.Pens.Pens.Select(k => $"{k.Key}={k.Value}")), $"{mon}:2=2");
            Check("MDATA7: the guildmaster", src.Guildmasters == null ? "none" : string.Join(",", src.Guildmasters.Held.Select(k => $"{k.Key}={k.Value.Name}/{k.Value.Level}")), "0=Boss/70");
            Check("MDATA8: square 3,4 of level 2 stood on (cell 92), its wall recorded, the been-here bit kept apart; deepest 4",
                  src.Automap == null ? "none" : $"{string.Join(",", src.Automap.Explored.Select(k => $"{k.Key}:{string.Join("/", k.Value)}"))} {src.Automap.SeenFlags(2, 2, 3, 30)} {src.DeepestReached}",
                  $"2:92 {1L << Floor.WALL_EAST} 4");
            Check("MDATA9: both entries, guild, day and text", src.GuildLog == null ? "none" : string.Join(" | ", src.GuildLog.Select(e => $"{e.Guild} {e.Day} {e.Text}")),
                  "1 46000 \"Synth\" joined our guild. | 0 46001 \"Second\" died on level 1.");
            var room = src.Dungeon?.LoadRoom(2, 3);
            Check("MDATA10: level 2 area 3, its group, stamp and chest; level 1's tally",
                  room == null ? "none" : $"{src.Dungeon!.Rooms.Count} {room.Foes[0].Ref} {room.Foes[0].GroupCount} {room.Foes[0].Stamp} {room.Foes[0].Engaged} {room.TreasureClass} {room.Lead} {room.Gold} {room.ChestLock} {src.Dungeon.Tally[1]}",
                  $"1 {mon} 2 3600.5 1 2 {mon} 5 3 77");
            Check("MDATA12: Synth's party", string.Join(" | ", src.Parties.Select(p => string.Join(",", p.Members))), "Synth,Second");
            var known = src.Library?.ForMonster(mon);
            Check("MDATA13: the creature's record by table index", known == null ? "none" : $"{src.Library!.Entries.Count} {known.Known} {known.NSeen} {known.Name} {known.LastSeen} {known.Location} {known.NDeaths}",
                  "1 4 3 Synth 46000 2 1");
            Check("MDATA14: the first record is record 1, the stamp overwritten (sub0595)", src.Hall == null ? "none" : $"{src.Hall.For(HallRecords.Kind.Strength).Value} {src.Hall.For(HallRecords.Kind.Strength).Holder} {src.Hall.For(HallRecords.Kind.Thieving).Value}",
                  "25 Synth 11");

            // ---- write it: into an empty saves folder, then again over itself
            var plan = SaveImport.Plan(src, saves);
            Check("the plan: both characters new, eight world files", $"{plan.Characters.Count(c => !c.Exists)} {plan.World.Count}", "2 8");
            var res = SaveImport.Apply(src, saves, new[] { "Synth", "Second" }, world: true, new DateTime(2026, 10, 25, 12, 0, 0));
            Check("nothing to back up in an empty folder", res.BackupDir ?? "none", "none");
            Check("written: the characters, Synth's party and the world",
                  string.Join(" ", res.Written), "Synth.json Second.json party-Synth.json automap.json dungeon.json store.json library.json records.json guildmasters.json confinement.json MDATA9.MDR");
            Check("the roster in record order", string.Join(",", Roster.Records(saves)), "Synth,Second");
            string json1 = File.ReadAllText(Path.Combine(saves, "Synth.json"));
            var loaded = Character.Load(Path.Combine(saves, "Synth.json"));
            string again = Path.Combine(root, "again.json");
            loaded.Save(again);
            Check("Synth loads in the port's Character code and saves again unchanged", File.ReadAllText(again) == json1, true);
            Check("... with its companion and its carry", $"{loaded.Companions[1]?.Name} {loaded.CarryingCompanion}", "Rex 1");
            var said = new List<string>();
            Party.LoadFor(Character.Load(Path.Combine(saves, "Synth.json")), saves, said.Add);
            Check("the party file reads with its leader (Party.LoadFor: Second is in town, Synth below)",
                  string.Join(" | ", said), Party.AutoloadPrefix + "\"Second\" is not here anymore!");
            Check("the guild log reads in the port's format (GuildLog.All)", string.Join(" | ", GuildLog.All(saves).Select(e => e.Text)), "\"Synth\" joined our guild. | \"Second\" died on level 1.");
            Check("the pens load keyed by Ref (Confinement.Load)", Confinement.Load(saves, monsters).Entries.Select(e => $"{e.Ref}:{(int)e.Align}={e.Count}").FirstOrDefault(), $"{mon}:2=2");
            Check("the dungeon keeps the deepest level from MDATA8", DungeonState.Load(saves).DeepestReached, 4);
            Check("the Library loads with no creature record left to settle", Librarian.Store.Load(saves, monsters).ForMonster(mon).Known, (short)4);

            var plan2 = SaveImport.Plan(src, saves);
            Check("a second import: both names now taken", plan2.Characters.Count(c => c.Exists), 2);
            File.WriteAllText(Path.Combine(saves, "Second.json"), File.ReadAllText(Path.Combine(saves, "Second.json")).Replace("\"Kills\": 0", "\"Kills\": 99"));
            var res2 = SaveImport.Apply(src, saves, new[] { "Synth" }, world: false, new DateTime(2026, 10, 25, 12, 0, 0));
            Check("... backed up first, into BACKUP\\before-import-<time>", res2.BackupDir != null && File.Exists(Path.Combine(res2.BackupDir, "Second.json"))
                  && Path.GetFileName(res2.BackupDir) == "before-import-20261025-120000", true);
            Check("... only the character confirmed is written; the other kept as it was", $"{string.Join(" ", res2.Written)} {Character.Load(Path.Combine(saves, "Second.json")).Kills}", "Synth.json party-Synth.json 99");
            var res3 = SaveImport.Apply(src, saves, Array.Empty<string>(), world: false, new DateTime(2026, 10, 25, 12, 0, 0));
            Check("... a second backup in the same second goes beside the first", res3.BackupDir != null && res3.BackupDir.EndsWith("-2"), true);
            Check("the DATA folder is untouched", Directory.GetFiles(data).Length, 10);
        }
        catch (Exception ex)
        {
            Check("the synthetic import ran", ex.GetType().Name + ": " + ex.Message, "no exception");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    /// <summary>Every declared type against MAIN.TXT, and every save file's Len against each Open in the modules.</summary>
    private static void ImportLayoutAgainstSource()
    {
        string main = Decompiled("decompiled-named", "MAIN.TXT");
        var types = new Dictionary<string, List<(string Name, string Kind, int Upper, int Str)>>();
        var block = new Regex(@"^Type (T[0-9A-F]+)\s*\r?\n(.*?)^End Type", RegexOptions.Multiline | RegexOptions.Singleline);
        var line = new Regex(@"^\s*(\w+)(?:\((\d+)\))?\s+As\s+(Integer|Long|Single|Double|Currency|String\s*\*\s*(\d+)|String|T[0-9A-F]+)\s*$");
        foreach (Match m in block.Matches(main))
        {
            var fields = new List<(string, string, int, int)>();
            foreach (string l in m.Groups[2].Value.Split('\n'))
            {
                var f = line.Match(l.TrimEnd('\r'));
                if (!f.Success) continue;
                string kind = f.Groups[4].Success ? "FixedString" : f.Groups[3].Value;
                fields.Add((f.Groups[1].Value, kind, f.Groups[2].Success ? int.Parse(f.Groups[2].Value) : -1, f.Groups[4].Success ? int.Parse(f.Groups[4].Value) : 0));
            }
            types[m.Groups[1].Value] = fields;
        }
        Console.WriteLine($"\nimporting: the declared types against decompiled-named/MAIN.TXT ({types.Count} Types read)");
        int SizeOf(string name) => types[name].Sum(f => (f.Upper < 0 ? 1 : f.Upper + 1) * f.Kind switch
        {
            "Integer" => 2, "Long" => 4, "Single" => 4, "Double" => 8, "Currency" => 8, "FixedString" => f.Str,
            "String" => throw new InvalidOperationException($"{name} holds a variable-length String"),
            _ => SizeOf(f.Kind),
        });
        foreach (var t in VbLayout.All)
        {
            if (!types.TryGetValue(t.Name, out var src)) { Check($"{t.Name} is declared in MAIN.TXT", "missing", "there"); continue; }
            string ours = string.Join(", ", t.Fields.Select(f => f.ToString()));
            string theirs = string.Join(", ", src.Select(f => f.Name + (f.Upper >= 0 ? $"({f.Upper})" : "") + " As "
                                                             + (f.Kind == "FixedString" ? $"String * {f.Str}" : f.Kind)));
            Check($"{t.Name}: field for field as MAIN.TXT declares it ({t.Fields.Count} fields)", ours == theirs ? "same" : ours, ours == theirs ? "same" : theirs);
            Check($"{t.Name}: {t.Size} bytes, as MAIN.TXT's declaration adds up", t.Size, SizeOf(t.Name));
        }

        // every Open of each file, in every module, and the Len it names
        Console.WriteLine("\nimporting: each file's Len against every Open ... For Random ... Len = in the modules");
        string dir = DecompiledDir("decompiled-named");
        var globals = Regex.Matches(main, @"Global Const (\w+) = (\d+)").ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));
        var files = new (string Const, string Name, int Len)[]
        {
            ("gc1C10", "MDATA4", VbLayout.CharactersLen), ("gc1C18", "MDATA6", VbLayout.StoreLen),
            ("gc1C1C", "MDATA7", VbLayout.GuildmastersLen), ("gc1C20", "MDATA8", VbLayout.AutomapLen),
            ("gc1C30", "MDATA10", VbLayout.RoomsLen), ("gc1C38", "MDATA12", VbLayout.PartiesLen),
            ("gc1C3C", "MDATA13", VbLayout.LibraryLen), ("gc1C40", "MDATA14", VbLayout.HallLen),
            ("gc1C44", "MDATA15", VbLayout.PensLen),
        };
        var basFiles = Directory.GetFiles(dir, "*.BAS").OrderBy(p => p).Select(p => (Name: Path.GetFileName(p), Lines: File.ReadAllLines(p))).ToList();
        foreach (var (cst, name, len) in files)
        {
            Check($"{cst} is {name}.MDR", globals.ContainsKey(cst) ? "" : Regex.Match(main, $@"Global Const {cst} = ""([^""]+)""").Groups[1].Value, name + ".MDR");
            var open = new Regex($@"Open .*\+ {cst}\b.*For Random.*Len = (\w+)");
            var seen = new SortedSet<int>();
            int opens = 0;
            foreach (var (bas, lines) in basFiles)
                for (int i = 0; i < lines.Length; i++)
                {
                    var m = open.Match(lines[i]);
                    if (!m.Success) continue;
                    opens++;
                    string c = m.Groups[1].Value;
                    int value = -1;
                    if (int.TryParse(c, out int lit)) value = lit;
                    else
                    {
                        // the nearest declaration above the Open: a routine's own (commented) Const, then the module's, then MAIN.TXT's
                        var decl = new Regex($@"^\s*'?\s*Const {c} = (\d+)");
                        for (int j = i; j >= 0 && value < 0; j--)
                        {
                            var d = decl.Match(lines[j]);
                            if (d.Success) value = int.Parse(d.Groups[1].Value);
                        }
                        if (value < 0 && globals.TryGetValue(c, out int g)) value = g;
                    }
                    seen.Add(value);
                }
            Check($"{name}: the Len of all {opens} Opens", string.Join(",", seen), len.ToString());
        }
    }

    /// <summary>A real DATA folder, when MORDOR_ORIGINAL_DATA names one.</summary>
    private static void RealImportChecks(string dir)
    {
        string? data = ImportProbe.DataDir(null);
        Console.WriteLine("\nimporting: a real DATA folder (MORDOR_ORIGINAL_DATA)");
        if (data == null)
        {
            Console.WriteLine("  skipped: no original DATA folder (set MORDOR_ORIGINAL_DATA to one, a copy - it is only read)");
            return;
        }
        var items = MdrLoader.LoadItems(Path.Combine(dir, "MDATA3.MDR"));
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        var before = Directory.GetFiles(data).ToDictionary(f => f, f => (new FileInfo(f).Length, File.GetLastWriteTimeUtc(f)));

        // sizes: the records whole, the last one exactly as long as what the game put there
        void Size(string file, int len, Func<VbFile, int> lastShouldBe, string what)
        {
            var f = VbFile.Open(Path.Combine(data, file), len);
            if (f == null) { Console.WriteLine($"  -     {file} absent"); return; }
            if (f.Length == 0) { Console.WriteLine($"  -     {file} empty"); return; }
            Check($"{file}: {f.RecordCount} records of {len}, the last {what}", f.LastRecordLength, lastShouldBe(f));
        }
        Size("MDATA4.MDR", VbLayout.CharactersLen, f => f.RecordCount <= 2 ? 2 : VbLayout.Character.Size, "a T5810 (2887), or the count when there is none");
        Size("MDATA6.MDR", VbLayout.StoreLen, _ => VbLayout.Shelf.Size, "a T5755 (14)");
        Size("MDATA15.MDR", VbLayout.PensLen, _ => VbLayout.Shelf.Size, "a T5755 (14)");
        Size("MDATA8.MDR", VbLayout.AutomapLen, _ => 4, "a Long (4)");
        Size("MDATA10.MDR", VbLayout.RoomsLen, _ => VbLayout.Fight.Size, "a T4F0B, the last room's Fight (16)");
        Size("MDATA13.MDR", VbLayout.LibraryLen, _ => VbLayout.Library.Size, "a T5BF1 (58)");
        Size("MDATA14.MDR", VbLayout.HallLen, _ => VbLayout.Hall.Size, "a T73E9 (42)");
        Size("MDATA7.MDR", VbLayout.GuildmastersLen, f => { VbFile.VarString(f.Record(f.RecordCount), 0, out int next); return next + 2; }, "a T564C (its name's length + 4)");
        Size("MDATA12.MDR", VbLayout.PartiesLen, f => { VbFile.VarString(f.Record(f.RecordCount), 0, out int next); return next; }, "a name (its length + 2)");

        OriginalSaves src;
        try { src = OriginalSaves.Read(data, items, monsters); }
        catch (Exception ex) { Check("the real DATA folder reads", ex.Message, "no exception"); return; }
        Console.WriteLine($"  {src.Characters.Count} characters, {src.Files.Count(f => f.Status.StartsWith("read"))} of {src.Files.Count} files read");
        foreach (string l in SaveImport.Summary(src)) Console.WriteLine("    " + l);
        foreach (var n in src.Notes) Console.WriteLine("    note: " + n);
        Check("nothing refused", src.Notes.Count(n => n.Kind == NoteKind.Refused), 0);
        Check("no value left unexplained (no warnings)", src.Notes.Count(n => n.Kind == NoteKind.Warning), 0);

        // the character walk against a second, plain one: every record from 3 with a name
        var f4 = VbFile.Open(Path.Combine(data, "MDATA4.MDR"), VbLayout.CharactersLen);
        if (f4 != null)
        {
            var names = new List<string>();
            for (int r = 3; r <= f4.RecordCount; r++)
            {
                string n = Encoding.Latin1.GetString(f4.Record(r), 0, 30).TrimEnd(' ', '\0');
                if (n != "") names.Add(n);
            }
            Check("MDATA4: the importer's walk is every named record (mdrmazGetChars)", string.Join(",", src.Characters.Select(c => c.Name)), string.Join(",", names.Take(f4.Integer(2))));
        }

        var byId = items.Select(i => i.Id).ToHashSet();
        var odd = new List<string>();
        foreach (var ic in src.Characters)
        {
            if (ic.Character is not Character c) continue;
            void Want(bool ok, string what) { if (!ok) odd.Add($"{c.Name}: {what}"); }
            Want(c.Stats.All(s => s is >= 1 and <= 99), "a stat out of 1..99");
            Want(c.MaxHits is >= 1 and <= 999 && c.CurHits >= 0 && (c.CurHits <= c.MaxHits || c.IsDead), $"hits {c.CurHits}/{c.MaxHits}");
            Want(c.Age >= 16 * 365, $"age {c.Age} days, under the 16 years a character starts at");
            Want(c.GoldOnHand >= 0 && c.GoldInBank >= 0, "negative gold");
            Want(c.DungeonLevel is >= 0 and <= 15 && c.X is >= 0 and < 30 && c.Y is >= 0 and < 30, $"position {c.DungeonLevel}:{c.X},{c.Y}");
            Want(c.Guilds.All(g => g.Level is >= 1 and <= 1000 && g.Experience >= 0), "a guild level or experience out of range");
            Want(c.Inventory.All(s => byId.Contains(s.ItemId)) && c.Bank.All(s => s.ItemId <= 0 || byId.Contains(s.ItemId)), "an item Id MDATA3 has not");
            Want(c.Companions.All.All(m => m.Ref >= 0 && m.Ref < monsters.Count && monsters[m.Ref].Id == m.MonsterId), "a companion whose Ref and Id disagree");
            Want(c.CreatedOn > 30000 && c.CreatedOn < 80000, $"a creation date serial of {c.CreatedOn}");
            // the offset seen when editing a copy: the current guild's level, read raw at 1678
            int current = BinaryPrimitives.ReadInt16LittleEndian(ic.Raw.AsSpan(1676));
            Want(BinaryPrimitives.ReadInt16LittleEndian(ic.Raw.AsSpan(1678 + 24 * current)) == c.Guild.Level, "the level at 1678 + 24 * CurrentGuild is not the current guild's");
        }
        Check("every character's values plausible (stats, hits, age, gold, position, guilds, items, companions, the level at 1678)", odd.Count == 0 ? "all" : string.Join("; ", odd), "all");
        if (src.GuildLog != null)
            Check("MDATA9: every entry's guild in MDATA1 and its day a date", src.GuildLog.All(e => e.Guild >= 0 && e.Guild < GuildData.All.Count && e.Day > 30000 && e.Day < 80000), true);

        // end to end into a scratch saves folder; each character loads and saves again unchanged
        string root = Path.Combine(Path.GetTempPath(), "mordor-selfcheck-realimport-" + Environment.ProcessId);
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            string saves = Path.Combine(root, "saves");
            var res = SaveImport.Apply(src, saves, src.Characters.Where(c => c.Character != null).Select(c => c.Name), world: true, DateTime.Now);
            var notClean = new List<string>();
            foreach (var ic in src.Characters.Where(c => c.Character != null))
            {
                string file = Path.Combine(saves, Character.SafeFileName(ic.Name) + ".json");
                try
                {
                    var c = Character.Load(file);
                    string again = Path.Combine(root, "again.json");
                    c.Save(again);
                    if (File.ReadAllText(again) != File.ReadAllText(file)) notClean.Add(ic.Name + " (saved differently)");
                }
                catch (Exception ex) { notClean.Add($"{ic.Name} ({ex.Message})"); }
            }
            Check($"each of the {src.Characters.Count(c => c.Character != null)} characters loads in the port and saves again unchanged", notClean.Count == 0 ? "all" : string.Join(", ", notClean), "all");
            Check("the roster in the original's record order", string.Join(",", Roster.Records(saves)), string.Join(",", src.Characters.Where(c => c.Character != null).Select(c => c.Name)));
            if (src.GuildLog != null) Check("the guild log reads back in the port's format, every entry", GuildLog.All(saves).Count, src.GuildLog.Count);
            if (src.Dungeon != null) Check("the rooms read back", DungeonState.Load(saves).Rooms.Count, src.Dungeon.Rooms.Count);
            if (src.Store != null) Check("the shelves read back", StoreStock.Load(saves).Shelf.Count, src.Store.Shelf.Count);
            if (src.Pens != null) Check("the pens read back keyed by Ref", Confinement.Load(saves, monsters).Entries.Count(), src.Pens.Pens.Count);
            if (src.Library != null) Check("the Library reads back", Librarian.Store.Load(saves, monsters).Entries.Count, src.Library.Entries.Count);
            if (src.Guildmasters != null) Check("the guildmasters read back, none appointed over them", Guildmasters.Load(saves).Held.Count, src.Guildmasters.Held.Count);
            if (src.Hall != null) Check("the Hall of Records reads back", HallRecords.Load(saves).Records.Count, src.Hall.Records.Count);
            Console.WriteLine($"  written: {string.Join(" ", res.Written)}");
        }
        catch (Exception ex) { Check("the real import ran end to end", ex.GetType().Name + ": " + ex.Message, "no exception"); }
        finally { try { Directory.Delete(root, true); } catch { } }

        var after = Directory.GetFiles(data).ToDictionary(f => f, f => (new FileInfo(f).Length, File.GetLastWriteTimeUtc(f)));
        Check("the DATA folder untouched (same files, sizes and times)", before.Count == after.Count && before.All(kv => after.TryGetValue(kv.Key, out var v) && v == kv.Value), true);
    }
}
