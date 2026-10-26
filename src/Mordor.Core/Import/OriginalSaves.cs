using System.Buffers.Binary;

namespace Mordor.Core.Import;

/// <summary>How much a note matters.</summary>
public enum NoteKind
{
    /// <summary>Expected and explained: a field the port works out for itself, a stale copy the original also ignores.</summary>
    Info,
    /// <summary>A value the port has no place for, or one that does not agree with the game data: imported as said, worth a look.</summary>
    Warning,
    /// <summary>Something not imported at all: a file refused, a character that cannot be read.</summary>
    Refused,
}

public sealed record ImportNote(NoteKind Kind, string Where, string Text)
{
    public override string ToString() => $"{Kind}: {Where}: {Text}";
}

/// <summary>One file of the DATA folder and what became of it.</summary>
public sealed record ImportFile(string Name, string Holds, string Status, int Records);

/// <summary>A character read out of MDATA4.</summary>
public sealed class ImportedCharacter
{
    /// <summary>The record it was in (from 1; record 3 is the first character).</summary>
    public int Record { get; init; }
    /// <summary><c>RTrim(Name)</c>, as the game names it.</summary>
    public string Name { get; init; } = "";
    /// <summary>The character in the port's model, or null when it cannot be imported (<see cref="Refusal"/>).</summary>
    public Character? Character { get; init; }
    public string? Refusal { get; init; }
    /// <summary>The raw record, for the checks.</summary>
    public byte[] Raw { get; init; } = Array.Empty<byte>();
}

/// <summary>A party MDATA12 remembers: the leader, then whoever was with them.</summary>
public sealed record ImportedParty(string Leader, List<string> Members);

/// <summary>
/// The original game's save data - its <c>DATA</c> folder - read into the port's model. Nothing is
/// written: every file is opened for reading only, and <see cref="SaveImport"/> does the writing,
/// into the port's saves folder. Which file holds what (gc1C10 to gc1C44, MAIN.TXT:580-593):
/// <list type="table">
/// <item><term>MDATA4</term><description>the characters, T5810 records (Len 2900) after a version and a count</description></item>
/// <item><term>MDATA6</term><description>the General Store's shelves, T5755 per item table index</description></item>
/// <item><term>MDATA7</term><description>the guildmasters, a name and a level per guild</description></item>
/// <item><term>MDATA8</term><description>the automap, one Long flag word per square, and the deepest level reached</description></item>
/// <item><term>MDATA9</term><description>the guild log (Binary, not records)</description></item>
/// <item><term>MDATA10</term><description>the rooms: each level's respawn tally and every area's four Foes() and Fight records</description></item>
/// <item><term>MDATA12</term><description>the remembered parties, four names per party</description></item>
/// <item><term>MDATA13</term><description>the Library, T5BF1 per monster and per item table index</description></item>
/// <item><term>MDATA14</term><description>the Hall of Records, T73E9 per record</description></item>
/// <item><term>MDATA15</term><description>the Confinement's pens, T5755 per monster table index</description></item>
/// </list>
/// MDATA1, 2, 3, 5 and 11 are the game's own tables and are not read here: the port reads its own
/// game folder's, and the items and monsters passed in are those.
/// </summary>
public sealed class OriginalSaves
{
    public string DataDir { get; }
    public List<ImportNote> Notes { get; } = new();
    public List<ImportFile> Files { get; } = new();
    public List<ImportedCharacter> Characters { get; } = new();
    /// <summary>MDATA4's record 2, the count of characters fn04B9 and mdrmazGetChars walk.</summary>
    public int CharacterCount { get; private set; }

    public DungeonState? Dungeon { get; private set; }
    public SharedMap? Automap { get; private set; }
    /// <summary>MDATA8's record 2; it lives in the port's dungeon.json, so it is kept apart until the two are put together.</summary>
    public int? DeepestReached { get; private set; }
    public StoreStock? Store { get; private set; }
    public Librarian.Store? Library { get; private set; }
    public HallRecords? Hall { get; private set; }
    public Confinement? Pens { get; private set; }
    public Guildmasters? Guildmasters { get; private set; }
    public List<GuildLog.Entry>? GuildLog { get; private set; }
    public List<ImportedParty> Parties { get; } = new();

    private readonly IReadOnlyList<Item> _items;
    private readonly IReadOnlyList<Monster> _monsters;
    private readonly Dictionary<short, Item> _itemById;

    private OriginalSaves(string dataDir, IReadOnlyList<Item> items, IReadOnlyList<Monster> monsters)
    {
        DataDir = dataDir;
        _items = items;
        _monsters = monsters;
        _itemById = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
    }

    private void Note(NoteKind k, string where, string text) => Notes.Add(new ImportNote(k, where, text));

    /// <summary>
    /// The folder the files are in: the one given, or its DATA folder when the game's own folder
    /// was picked (the original keeps them in <c>BasePath + "DATA\"</c>, gc1C74).
    /// </summary>
    public static string? FindDataDir(string picked)
    {
        if (File.Exists(Path.Combine(picked, "MDATA4.MDR"))) return picked;
        string sub = Path.Combine(picked, "DATA");
        if (File.Exists(Path.Combine(sub, "MDATA4.MDR"))) return sub;
        return null;
    }

    /// <summary>
    /// Read a DATA folder. <see cref="GuildData"/> and <see cref="RaceData"/> must be loaded (they
    /// name the guilds and races the original keeps by number); <paramref name="items"/> and
    /// <paramref name="monsters"/> are MDATA3 and MDATA5 in table order, as <see cref="MdrLoader"/>
    /// reads them. Never throws for a bad file: what could not be read is a note.
    /// </summary>
    public static OriginalSaves Read(string dataDir, IReadOnlyList<Item> items, IReadOnlyList<Monster> monsters)
    {
        var s = new OriginalSaves(dataDir, items, monsters);
        s.Guard("MDATA4.MDR", "characters", s.ReadCharacters);
        s.Guard("MDATA6.MDR", "the General Store's shelves", s.ReadStore);
        s.Guard("MDATA7.MDR", "the guildmasters", s.ReadGuildmasters);
        s.Guard("MDATA8.MDR", "the automap and the deepest level reached", s.ReadAutomap);
        s.Guard("MDATA9.MDR", "the guild log", s.ReadGuildLog);
        s.Guard("MDATA10.MDR", "the dungeon's rooms and respawn tallies", s.ReadRooms);
        s.Guard("MDATA12.MDR", "the remembered parties", s.ReadParties);
        s.Guard("MDATA13.MDR", "the Library", s.ReadLibrary);
        s.Guard("MDATA14.MDR", "the Hall of Records", s.ReadHall);
        s.Guard("MDATA15.MDR", "the Confinement's pens", s.ReadPens);
        s.CompareTables();
        return s;
    }

    private void Guard(string file, string holds, Func<string, string, int> read)
    {
        string path = Path.Combine(DataDir, file);
        if (!File.Exists(path)) { Files.Add(new ImportFile(file, holds, "absent", 0)); return; }
        try
        {
            int records = read(path, file);
            if (!Files.Any(f => f.Name == file)) Files.Add(new ImportFile(file, holds, "read", records));
        }
        catch (Exception ex)
        {
            Files.Add(new ImportFile(file, holds, "refused: " + ex.Message, 0));
            Note(NoteKind.Refused, file, "could not be read: " + ex.Message);
        }
    }

    private void Refuse(string file, string holds, string why)
    {
        Files.Add(new ImportFile(file, holds, "refused: " + why, 0));
        Note(NoteKind.Refused, file, why);
    }

    /// <summary>The version stamp in record 1, checked as fn0326 checks it; false (and the file refused) when the game would refuse it.</summary>
    private bool StampOk(VbFile f, string file, string holds)
    {
        string stamp = f.VarString(1);
        if (VbLayout.VersionAccepted(stamp)) return true;
        Refuse(file, holds, $"stamped with version \"{stamp}\", which Mordor {VbLayout.Version} refuses (fn0326)");
        return false;
    }

    /// <summary>MDATA3 and MDATA5 beside the saves against the port's game folder's: the saves index both.</summary>
    private void CompareTables()
    {
        void Compare(string file, int len, int count, string what)
        {
            var f = VbFile.Open(Path.Combine(DataDir, file), len);
            if (f == null) return;
            int theirs = f.Integer(3);
            if (theirs != count)
                Note(NoteKind.Warning, file, $"this DATA folder's {what} table has {theirs} entries and the port's game folder's {count}: "
                     + "the saves index the table by position, so they were made with other game data");
        }
        Compare("MDATA3.MDR", 125, _items.Count, "item");
        Compare("MDATA5.MDR", 160, _monsters.Count, "monster");
    }

    // ------------------------------------------------------------------ MDATA4, the characters

    private int ReadCharacters(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.CharactersLen)!;
        if (!StampOk(f, file, "characters")) return f.RecordCount;
        CharacterCount = f.Integer(2);
        // mdrmazGetChars (MAZEMOD.BAS:203-224): the count's worth of records from 3 on, a blank
        // (deleted) record skipped without counting
        int rec = 3, taken = 0;
        while (rec <= f.RecordCount)
        {
            var raw = f.Record(rec);
            var r = new VbRecord(raw, VbLayout.Character);
            string name = VbFile.RTrim(r.FixedString("Name"));
            if (name != "")
            {
                if (taken >= CharacterCount)
                    Note(NoteKind.Warning, $"{file} record {rec}", $"holds \"{name}\" past the count of {CharacterCount}: the original lists no record past its count, so it is not imported");
                else
                {
                    taken++;
                    Characters.Add(Convert(rec, name, raw));
                }
            }
            rec++;
        }
        if (taken < CharacterCount)
            Note(NoteKind.Warning, file, $"the count says {CharacterCount} characters and the file holds {taken}");
        var dup = Characters.GroupBy(c => Character.SafeFileName(c.Name), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
        foreach (var g in dup)
            Note(NoteKind.Warning, file, $"{g.Count()} characters would share the file name \"{g.Key}.json\"; only the first is imported");
        return f.RecordCount;
    }

    private ImportedCharacter Convert(int recordNo, string name, byte[] raw)
    {
        string where = $"\"{name}\"";
        var r = new VbRecord(raw, VbLayout.Character);
        ImportedCharacter Refused(string why)
        {
            Note(NoteKind.Refused, where, why);
            return new ImportedCharacter { Record = recordNo, Name = name, Raw = raw, Refusal = why };
        }

        int race = r.Integer("RaceIndex");
        if (race < 0 || race >= RaceData.All.Count) return Refused($"race {race} is not in MDATA1");
        int current = r.Integer("CurrentGuild");
        if (current < 0 || current >= GuildData.All.Count) return Refused($"current guild {current} is not in MDATA1");

        var c = new Character
        {
            Name = name,
            Race = RaceData.All[race].Name,
            StatMax = (int[])RaceData.All[race].Max.Clone(),
        };

        int align = r.Integer("Alignment");
        if (align is < 0 or > 2) { Note(NoteKind.Warning, where, $"alignment {align} is not Good, Neutral or Evil (gv074C); taken as Neutral"); align = 1; }
        c.Alignment = (Alignment)align;
        int sex = r.Integer("M5829");
        if (sex is < 0 or > 1) { Note(NoteKind.Warning, where, $"sex {sex} is not Male or Female (gv06B6); taken as Male"); sex = 0; }
        c.Sex = (Sex)sex;

        for (int i = 0; i < 6; i++) c.Stats[i] = r.Integer($"Stats({i})");
        if (r.Integer("Stats(6)") != 0) Note(NoteKind.Warning, where, $"Stats(6) holds {r.Integer("Stats(6)")}; nothing reads a seventh stat and the port has none");

        float age = r.Single("Age");
        c.Age = (int)Math.Round(age, MidpointRounding.ToEven);
        if (c.Age != age) Note(NoteKind.Info, where, $"age {age} days is kept as {c.Age} (the port counts whole days)");

        c.MaxHits = r.Integer("MaxHits");
        c.CurHits = r.Integer("Hits");
        c.CurSpellPoints = r.Integer("SpellPoints");

        // where: X and Y are 1-based in the record and 0-based in the port (DungeonWindow shows X + 1)
        c.DungeonLevel = r.Integer("DungeonLevel");
        int x = r.Integer("X"), y = r.Integer("Y");
        if (x < 1 || y < 1) Note(NoteKind.Warning, where, $"position {x},{y} is off the map's 1-based squares; kept as 0,0");
        c.X = Math.Max(0, x - 1);
        c.Y = Math.Max(0, y - 1);
        int facing = r.Integer("Facing");
        if (facing is < 1 or > 4) { Note(NoteKind.Warning, where, $"facing {facing} is not 1 to 4 (gv06E8 + 1); taken as North"); facing = 1; }
        c.Facing = facing;
        c.Area = r.Integer("M59BB");
        c.SanctuaryX = r.Integer("Sanctuary.M4E78");
        c.SanctuaryY = r.Integer("Sanctuary.M4E7D");
        c.SanctuaryLevel = r.Integer("Sanctuary.Level");
        c.LostLevel = r.Integer("M59F4(0)") != 0;
        c.LostPosition = r.Integer("M59F4(1)") != 0;
        c.LostFacing = r.Integer("M59F4(2)") != 0;

        c.GoldOnHand = r.Currency("Gold");
        c.GoldInBank = r.Currency("BankGold");

        // ---- the guilds: sixteen slots by guild index; Level > 0 is joined (FRMCTYG.BAS:98-103)
        var joined = new List<GuildMembership>();
        for (int g = 0; g < 16; g++)
        {
            var gr = r.Sub($"GuildsJoined({g})");
            int level = gr.Integer("Level");
            if (level <= 0)
            {
                if (level < 0 || !gr.IsZero && g != current)
                    Note(NoteKind.Info, where, $"guild slot {g} holds a level of {level} and other values; not joined, so not imported");
                continue;
            }
            if (g >= GuildData.All.Count) { Note(NoteKind.Warning, where, $"guild slot {g} has level {level} but MDATA1 has only {GuildData.All.Count} guilds; dropped"); continue; }
            // the current guild's experience is kept on the character itself, M579B, and copied
            // back into its slot only when the guild changes (FRMCTYG.BAS:148-150)
            var exp = g == current ? r.Currency("M579B") : gr.Currency("M579B");
            var m = new GuildMembership
            {
                Guild = GuildData.All[g].Name,
                Level = level,
                Experience = exp.RoundToWhole(),
                QuestType = gr.Integer("M55CB"),
                QuestTarget = gr.Integer("M57A9"),
                QuestDone = gr.Integer("M57B2") != 0,
            };
            if (exp.Raw % VbCurrency.Scale != 0) Note(NoteKind.Info, where, $"{m.Guild} experience {exp} is kept as {m.Experience}");
            var (pa, pd) = Rules.GuildADSingle(m.Guild, level);
            float sa = gr.Single("Att"), sd = gr.Single("Def");
            if ((int)sa != (int)pa || (int)sd != (int)pd)
                Note(NoteKind.Warning, where, $"{m.Guild} level {level}: the record's guild A/D is {(int)sa}/{(int)sd}, the port works out {(int)pa}/{(int)pd} from the level (it keeps no guild A/D of its own)");
            if (g == current) joined.Insert(0, m); else joined.Add(m);
        }
        if (joined.Count == 0 || joined[0].Guild != GuildData.All[current].Name)
            return Refused($"the current guild, {GuildData.All[current].Name}, has no level: the record joins no guild the port can make current");
        c.Guilds = joined;
        if (joined.Count > 1)
            Note(NoteKind.Info, where, "the current guild comes first and the rest follow by guild index (the port keeps guilds as a list, the original by index)");

        // ---- the pack: 40 slots, 0 the Hands; Ref < 1 past slot 0 is empty (MAINMOD2.BAS:1723)
        for (int i = 1; i < Character.PackSize; i++)
            c.PutInSlot(i, SlotFrom(r.Sub($"Pack({i})"), where, $"pack slot {i + 1}"));
        var hands = r.Sub("Pack(0)");
        if (hands.Integer("Ref") != 0 || hands.Integer("Id") != 0)
            Note(NoteKind.Warning, where, $"pack slot 1 (the Hands) holds item Ref {hands.Integer("Ref")} Id {hands.Integer("Id")}; the port's slot 1 is always the Hands");
        if (!r.Sub("Pack(40)").IsZero) Note(NoteKind.Warning, where, "Pack(40), past the forty slots mc0190 walks, is not empty; nothing reads it");

        // ---- the bank: 40 slots (mc00A2); an emptied one stays a hole (FRMCITYB.BAS:187-199)
        var bank = new List<InventorySlot>();
        for (int i = 0; i < Character.BankSlots; i++)
            bank.Add(SlotFrom(r.Sub($"M5860({i})"), where, $"bank slot {i + 1}") ?? new InventorySlot());
        while (bank.Count > 0 && bank[^1].ItemId <= 0) bank.RemoveAt(bank.Count - 1);
        c.Bank = bank;
        if (!r.Sub("M5860(40)").IsZero) Note(NoteKind.Warning, where, "M5860(40), past the bank's forty slots, is not empty; nothing reads it");

        // ---- companions: Ref > -1 is a companion; an empty slot keeps what its last one left
        // (MAINMOD2.BAS:383-400, 4745)
        for (int s = 0; s < CompanionSlots.Slots; s++)
        {
            var cr = r.Sub($"Companions({s})");
            int cref = cr.Integer("Ref");
            string cname = VbFile.RTrim(cr.FixedString("Name"));
            int calign = cr.Integer("Alignment");
            var comp = new Companion
            {
                Ref = cref,
                MonsterId = cr.Integer("M57D4"),
                Name = cname == "" ? " " : cname,
                Hits = cr.Integer("Hits"),
                MaxHits = cr.Integer("MaxHits"),
                // the foe's alignment, 0 Good to 2 Evil (fn1163, MAINMOD2.BAS:392); the port keeps
                // the item scheme, one more (CombatMonster.CompanionAlignment)
                Alignment = calign is >= 0 and <= 2 ? (ItemAlignment)(calign + 1) : ItemAlignment.Unaligned,
                IdLevel = cr.Integer("IdLevel"),
                BindingLevel = cr.Integer("BindingLevel"),
            };
            if (cref > -1)
            {
                if (calign is < 0 or > 2) Note(NoteKind.Warning, where, $"companion {s + 1}'s alignment {calign} is not 0 to 2; kept as Unaligned");
                if (cref >= _monsters.Count)
                    Note(NoteKind.Warning, where, $"companion {s + 1} is monster table index {cref}, past MDATA5's {_monsters.Count}; the port settles it from its Id {comp.MonsterId} on load");
                else
                {
                    var mon = _monsters[cref];
                    if (mon.Id != comp.MonsterId)
                        Note(NoteKind.Warning, where, $"companion {s + 1}: table index {cref} is {mon.Name} (Id {mon.Id}) but the record's Id is {comp.MonsterId}");
                    if (cr.Integer("Att") != mon.Att || cr.Integer("Def") != mon.Def)
                        Note(NoteKind.Info, where, $"companion {s + 1} ({mon.Name}) carries A/D {cr.Integer("Att")}/{cr.Integer("Def")}; the port uses the monster's {mon.Att}/{mon.Def} and keeps none per companion");
                }
                c.Companions[s] = comp;
            }
            else
            {
                comp.Ref = -1;
                c.Companions.SetRemains(s, comp);
            }
        }
        if (!r.Sub("Companions(4)").IsZero && r.Sub("Companions(4)").Integer("Ref") > -1)
            Note(NoteKind.Warning, where, "Companions(4), a fifth slot past the four mc00E0 walks, holds a companion; nothing reads it");

        // ---- carrying: -1 nothing, 0 to 3 a companion, 4 and up a body from the morgue list; fn04CD
        // drops a carried body when the record is loaded (MAINMOD.BAS:354-356), and so does this
        int carry = r.Integer("M5943");
        c.CarryingCompanion = carry is >= 0 and < CompanionSlots.Slots ? carry : -1;
        c.CarryingCharacter = "";
        if (carry >= CompanionSlots.Slots)
            Note(NoteKind.Info, where, "was carrying a body; the original puts it down when the character is next loaded (fn04CD), and so does the import");

        // ---- ailments: Ailments(0) poison, (1) disease, (2) paralysis, each held as a level, and
        // (3) to (6) the four visible states (MAINMOD2.BAS:51-65, sub048C)
        int poison = r.Integer("Ailments(0)"), disease = r.Integer("Ailments(1)");
        c.PoisonLevel = poison; c.Poisoned = poison != 0;
        c.DiseaseLevel = disease; c.Diseased = disease != 0;
        c.ParalysisLevel = r.Integer("Ailments(2)");
        c.Levitating = r.Integer("Ailments(3)") != 0;
        c.InvisibleNow = r.Integer("Ailments(4)") != 0;
        c.SeeingInvisible = r.Integer("Ailments(5)") != 0;
        c.Protection = r.Integer("Ailments(6)");
        if (r.Integer("Ailments(7)") != 0) Note(NoteKind.Warning, where, $"Ailments(7) holds {r.Integer("Ailments(7)")}; nothing reads it");
        c.SpellEffects = r.Long("SpellEffects");
        for (int i = 0; i < Effects.ResistanceKinds; i++) c.SpellResist[i] = r.Integer($"SpellResistances({i})");

        // ---- death: DeathState 0 alive, 1 Dead, 2 Stoned ... 8 Rock (gv0814); the port keeps the
        // numbering in CauseOfDeath but writes 0 for an ordinary death (DeathStates.Name)
        int death = r.Integer("DeathState");
        if (death is < 0 or > 8) { Note(NoteKind.Warning, where, $"DeathState {death} is not 0 to 8; taken as Dead"); death = 1; }
        c.IsDead = death != 0;
        c.CauseOfDeath = death <= 1 ? Rules.DeathOrdinary : death;
        c.Stoned = death == Rules.DeathStoned;
        c.RaiseKind = r.Integer("M58DC");
        c.RaiseChance = r.Integer("M58E9");
        c.RaisedBy = VbFile.RTrim(r.FixedString("M58F9"));

        // ---- the tallies, M5906: 0 kills, 1 deaths, 2 complications, 3 and 4 quests, 5 seconds
        // played, 6 the creation date (CHARCREA.BAS:248, MAINMOD2.BAS:5262-5290)
        int Whole(int i) => (int)Math.Clamp(r.Currency($"M5906({i})").RoundToWhole(), int.MinValue, int.MaxValue);
        c.Kills = Whole(0);
        c.Deaths = Whole(1);
        c.Complications = Whole(2);
        c.MonsterQuestsDone = Whole(3);
        c.ItemQuestsDone = Whole(4);
        c.PlaySeconds = r.Currency("M5906(5)").ToDouble();
        c.CreatedOn = r.Currency("M5906(6)").ToDouble();
        for (int i = 7; i <= 8; i++)
            if (r.Currency($"M5906({i})").Raw != 0) Note(NoteKind.Warning, where, $"M5906({i}) holds {r.Currency($"M5906({i})")}; nothing reads it");

        // ---- the Control Panel's options, M5914: 0 Auto Change Level, 2 Hide, 3 Preload, 4 No
        // Perception (FRMCONTR.BAS:157-160), 5 the Combat Setting (FRMPINFO.BAS:16, 53)
        c.Options = new CharacterOptions
        {
            AutoChangeLevel = r.Integer("M5914(0)") != 0,
            Hide = r.Integer("M5914(2)") != 0,
            PreloadCharacter = r.Integer("M5914(3)") != 0,
            NoPerceptionMessages = r.Integer("M5914(4)") != 0,
        };
        c.CombatSetting = r.Integer("M5914(5)");

        // the password, M594C, compared RTrim'd; a declined one is a lone space (CHARCREA.BAS:170)
        string pw = VbFile.RTrim(r.FixedString("M594C"));
        c.Password = pw == "" ? Character.NoPassword : pw;

        // ---- the windows, M5958, in twips; the port keeps pixels at 96 dpi (WindowLayout.TwipsPerPixel)
        var win = WindowLayout.Fresh();
        for (int i = 0; i < win.Length && i < 21; i++)
        {
            var w = r.Sub($"M5958({i})");
            win[i] = new WindowRect
            {
                Left = w.Long("M425F") / (double)WindowLayout.TwipsPerPixel,
                Top = w.Long("M4258") / (double)WindowLayout.TwipsPerPixel,
                Width = w.Long("M4216") / (double)WindowLayout.TwipsPerPixel,
                Height = w.Long("M420C") / (double)WindowLayout.TwipsPerPixel,
                State = w.Integer("M5801"),
            };
        }
        c.Windows = win;
        if (!r.Sub("M5958(20)").IsZero) Note(NoteKind.Info, where, "window placement 20 is not empty; no form uses it and the port keeps twenty");

        // ---- spell buffers, M59B0(0 to 9): the same numbers in the port (SpellBuffers)
        for (int i = 0; i < SpellBuffers.Count; i++) c.Buffers[i] = r.Integer($"M59B0({i})");
        int b10 = r.Integer("M59B0(10)");
        if (b10 != 0 && b10 != -1) Note(NoteKind.Info, where, $"M59B0(10), an eleventh buffer no key reaches, holds {b10}");

        c.Swings = r.Integer("M56AF");

        // ---- what the port works out for itself: noted when the record's figure is not what the
        // port would work out, so nothing changes silently
        c.MaxSpellPoints = Spellbook.MaxSpellPoints(c);
        if (c.CurSpellPoints > c.MaxSpellPoints)
            Note(NoteKind.Warning, where, $"{c.CurSpellPoints} spell points against the {c.MaxSpellPoints} (INT + WIS) * 5 allows; kept");
        int swings = c.Swings;
        c.RecalcSwings();
        if (c.Swings != swings) Note(NoteKind.Info, where, $"extra swings {swings} in the record; the port's load works out {c.Swings} from the guild levels");
        c.Swings = swings;

        Item? Lookup(short id) => _itemById.TryGetValue(id, out var it) ? it : null;
        for (int i = 0; i < 6; i++)
        {
            int mods = c.StatWithMods(i, Lookup) - c.Stats[i], stored = r.Integer($"StatMods({i})");
            if (mods != stored)
                Note(NoteKind.Warning, where, $"{RaceData.StatNames[i]} modifier: the record has {stored}, the port works out {mods} from the equipped gear (it keeps none of its own)");
        }
        if (r.Integer("StatMods(6)") != 0) Note(NoteKind.Warning, where, $"StatMods(6) holds {r.Integer("StatMods(6)")}; nothing reads it");

        // Att and Def are running totals in the record; the port works them out and keeps only
        // what the totals hold beyond that, ADShift (see Character.ADShift)
        int statsAD = c.StatsADNow(Lookup);
        var (ga, gd) = Rules.BestGuildAD(c);
        int ia = c.Inventory.Where(s => s.Equipped).Sum(s => Lookup(s.ItemId)?.Att ?? 0);
        int id = c.Inventory.Where(s => s.Equipped).Sum(s => Lookup(s.ItemId)?.Def ?? 0);
        int att = (int)Math.Round(r.Single("Att"), MidpointRounding.ToEven), def = (int)Math.Round(r.Single("Def"), MidpointRounding.ToEven);
        int shiftA = att - (Rules.BaseAD + statsAD + ga + ia), shiftD = def - (Rules.BaseAD + statsAD + gd + id);
        if (shiftA == shiftD)
        {
            c.ADShift = shiftA;
            if (shiftA != 0)
                Note(NoteKind.Info, where, $"Att/Def {att}/{def} are {shiftA:+0;-0} beyond base, best guild, stats and gear; kept as the port's ADShift"
                     + (c.ParalysisLevel != 0 ? " (paralysed: the stat part is off while held)" : ""));
        }
        else
        {
            c.ADShift = 0;
            Note(NoteKind.Warning, where, $"Att/Def {att}/{def} differ from what the port works out by {shiftA} and {shiftD}, which one ADShift cannot hold; the port's own figures are used");
        }
        long cap = Rules.Fn13ED(c.Guild.Level, c.ActiveGuildDef.ExpFactor, c.RaceDef.ExpFactor);
        if (r.Long("M5970") != cap)
            Note(NoteKind.Info, where, $"the stored experience cap M5970 is {r.Long("M5970")}, fn13ED gives {cap}; the port works it out each time");

        // fields the record has and nothing reads: say so if one is not empty
        foreach (var unused in new[] { "M56F7", "M58A6", "M59CB", "M59D9" })
            if (r.Integer(unused) != 0) Note(NoteKind.Info, where, $"{unused} holds {r.Integer(unused)}; nothing in the game reads it");
        foreach (var unused in new[] { "M587C", "M588A" })
            if (r.Single(unused) != 0) Note(NoteKind.Info, where, $"{unused} holds {r.Single(unused)}; nothing in the game reads it");
        if (r.Integer("M5962") != recordNo)
            Note(NoteKind.Info, where, $"M5962, the record number the game keeps while loaded, says {r.Integer("M5962")}; the character is in record {recordNo}");

        return new ImportedCharacter { Record = recordNo, Name = name, Raw = raw, Character = c };
    }

    /// <summary>A pack or bank slot (T576E), or null for an empty one (Ref below 1).</summary>
    private InventorySlot? SlotFrom(VbRecord s, string where, string which)
    {
        int rf = s.Integer("Ref");
        if (rf < 1) return null;
        short id = s.Integer("Id");
        if (rf < _items.Count)
        {
            var it = _items[rf];
            if (it.Id != id)
                Note(NoteKind.Warning, where, $"{which}: table index {rf} is {it.Name} (Id {it.Id}) but the slot's Id is {id}; the table's is used, as the game reads Items(Ref)");
            id = it.Id;
        }
        else
        {
            Note(NoteKind.Warning, where, $"{which}: table index {rf} is past MDATA3's {_items.Count} items; kept by its Id {id}"
                 + (_itemById.ContainsKey(id) ? "" : ", which no item has"));
        }
        int known = s.Integer("IdLevel");
        if (known is < 0 or > 3) { Note(NoteKind.Warning, where, $"{which}: ID level {known} is not 0 to 3; taken as fully known"); known = 0; }
        int align = s.Integer("Alignment");
        if (align is < 0 or > 3) { Note(NoteKind.Warning, where, $"{which}: alignment {align} is not U, G, N or E; taken as Unaligned"); align = 0; }
        return new InventorySlot
        {
            ItemId = id,
            Equipped = s.Integer("Equipped") != 0,
            Charges = s.Integer("Charges"),
            // the original counts identification down, 0 complete (StoreStock.IdCountdown)
            Known = (IdLevel)(3 - known),
            SlotCursed = s.Integer("Cursed") != 0,
            Alignment = (ItemAlignment)align,
        };
    }

    // ------------------------------------------------------------------ MDATA6, the store

    private int ReadStore(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.StoreLen)!;
        if (!StampOk(f, file, "the General Store's shelves")) return f.RecordCount;
        int n = f.Integer(3);
        if (n != _items.Count)
            Note(NoteKind.Warning, file, $"has {n} shelves and MDATA3 {_items.Count} items; each shelf is taken by its item Id, as fn0425 re-maps a store file");
        var stock = new StoreStock();
        for (int i = 0; i < n && f.Has(i + 4); i++)
        {
            var s = f.Record(i + 4, VbLayout.Shelf);
            short id = s.Integer("Id");
            if (id == 0) continue;
            if (!_itemById.ContainsKey(id)) { Note(NoteKind.Warning, file, $"shelf {i} holds item Id {id}, which MDATA3 has not; dropped"); continue; }
            if (n == _items.Count && (i >= _items.Count || _items[i].Id != id))
                Note(NoteKind.Warning, file, $"shelf {i} holds Id {id} where MDATA3's item {i} is Id {(i < _items.Count ? _items[i].Id : -1)}; taken by its Id");
            for (int a = 0; a < 4; a++)
            {
                int count = s.Integer($"Align({a})");
                if (count > 0) stock.Shelf[$"{id}:{a}"] = count;
                else if (count < 0) Note(NoteKind.Warning, file, $"item Id {id}: a count of {count} at alignment {a}; dropped");
            }
            if (s.Integer("Align(4)") != 0) Note(NoteKind.Warning, file, $"item Id {id}: Align(4) holds {s.Integer("Align(4)")}; the store has four alignments (gc03E6)");
        }
        Store = stock;
        return f.RecordCount;
    }

    // ------------------------------------------------------------------ MDATA15, the pens

    private int ReadPens(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.PensLen)!;
        if (!StampOk(f, file, "the Confinement's pens")) return f.RecordCount;
        int n = f.Integer(3);
        if (n != _monsters.Count)
            Note(NoteKind.Warning, file, $"has {n} pens and MDATA5 {_monsters.Count} monsters; each pen is taken by its creature's Id, as fn0425 re-maps a companion store file");
        var pens = new Confinement { KeyedBy = Confinement.ByRef };
        for (int i = 0; i < n && f.Has(i + 4); i++)
        {
            var s = f.Record(i + 4, VbLayout.Shelf);
            short id = s.Integer("Id");
            if (id == 0 && s.IsZero) continue;
            int at = i < _monsters.Count && _monsters[i].Id == id ? i : -1;
            if (at < 0)
            {
                at = _monsters.ToList().FindIndex(m => m.Id == id);
                if (at < 0) { Note(NoteKind.Warning, file, $"pen {i} holds monster Id {id}, which MDATA5 has not; dropped"); continue; }
                Note(NoteKind.Warning, file, $"pen {i} holds Id {id}; filed under table index {at}, the first creature with it");
            }
            // Align(0 to 2), the monster alignments; the port's key is the item scheme, one more
            for (int a = 0; a < 3; a++)
            {
                int count = s.Integer($"Align({a})");
                if (count > 0) pens.Pens[$"{at}:{a + 1}"] = count;
            }
            for (int a = 3; a <= 4; a++)
                if (s.Integer($"Align({a})") != 0) Note(NoteKind.Warning, file, $"{_monsters[at].Name}: Align({a}) holds {s.Integer($"Align({a})")}; a creature is Good, Neutral or Evil");
        }
        Pens = pens;
        return f.RecordCount;
    }

    // ------------------------------------------------------------------ MDATA7, the guildmasters

    private int ReadGuildmasters(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.GuildmastersLen)!;
        if (!StampOk(f, file, "the guildmasters")) return f.RecordCount;
        // T564C, a variable-length Name and a Level, one record per guild from record 2 (MODULE1.BAS reads them in turn)
        var gm = new Guildmasters();
        for (int g = 0; g < GuildData.All.Count; g++)
        {
            if (!f.Has(g + 2)) { Note(NoteKind.Warning, file, $"has no record for {GuildData.All[g].Name}; the port appoints its own guildmaster there"); continue; }
            var rec = f.Record(g + 2);
            string name = VbFile.VarString(rec, 0, out int next);
            int level = next + 2 <= rec.Length ? BinaryPrimitives.ReadInt16LittleEndian(rec.AsSpan(next)) : 0;
            if (name == "") continue;
            gm.Held[g] = new Guildmasters.Holder { Name = name, Level = level };
        }
        Guildmasters = gm;
        return f.RecordCount;
    }

    // ------------------------------------------------------------------ MDATA8, the automap

    /// <summary>gc1D50: the automap's been-here bit, set on every square stood on (MAZEMOD.BAS:1626).</summary>
    public const int BeenHereBit = 23;

    private int ReadAutomap(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.AutomapLen)!;
        if (!StampOk(f, file, "the automap")) return f.RecordCount;
        DeepestReached = f.Integer(2);
        // fn0A34 (MAZEMOD.BAS:732): level L's square x, y (from 1) is the Long in record
        // (L - 1) * 900 + x + 30 * (y - 1) + 2, 900 being mc01AC * mc01AE; fn0B01's row stride is
        // the level's width, 30 on every level of the game
        const int W = 30, H = 30;
        var map = new SharedMap();
        for (int level = 1; level <= 15; level++)
        {
            var cells = new List<int>();
            for (int i = 1; i <= W * H; i++)
            {
                int rec = (level - 1) * W * H + i + 2;
                if (!f.Has(rec)) break;
                long word = (uint)BinaryPrimitives.ReadInt32LittleEndian(f.Record(rec));
                if (word == 0) continue;
                int portIndex = i - 1;   // (y - 1) * 30 + (x - 1), the port's 0-based y * width + x
                string key = $"{level}:{portIndex}";
                if ((word & (1L << BeenHereBit)) != 0) cells.Add(portIndex);
                long flags = word & ~(1L << BeenHereBit);
                if (flags != 0)
                {
                    map.Seen[key] = flags;
                    // the original asks the same word whether a square is already worked out
                    // (mdrmazPerceiveSquare, MAZEMOD.BAS:632-634); the port asks Noticed
                    var bits = new List<int>();
                    for (int b = 0; b < 32; b++) if ((flags & (1L << b)) != 0) bits.Add(b);
                    map.Noticed[key] = bits;
                }
            }
            if (cells.Count > 0) map.Explored[level] = cells;
        }
        Automap = map;
        return f.RecordCount;
    }

    // ------------------------------------------------------------------ MDATA9, the guild log

    private int ReadGuildLog(string path, string file)
    {
        // Binary, not records (sub0578, HALL.BAS:25-50): an Integer count, then each entry an
        // Integer guild, a Long day (CLng(Date)), an Integer length and the text. The port's own
        // MDATA9.MDR keeps the same fields wider (Mordor.Core.GuildLog), so it is converted.
        byte[] d = File.ReadAllBytes(path);
        var entries = new List<GuildLog.Entry>();
        if (d.Length >= 2)
        {
            int count = BinaryPrimitives.ReadInt16LittleEndian(d);
            int at = 2;
            while (at + 8 <= d.Length)
            {
                int guild = BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(at));
                int day = BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(at + 2));
                int len = BinaryPrimitives.ReadInt16LittleEndian(d.AsSpan(at + 6));
                if (len < 0 || at + 8 + len > d.Length) { Note(NoteKind.Warning, file, $"entry {entries.Count + 1} runs past the end of the file; the log is taken up to it"); break; }
                entries.Add(new GuildLog.Entry(guild, day, VbFile.Text(d.AsSpan(at + 8, len))));
                at += 8 + len;
            }
            if (count != entries.Count)
                Note(NoteKind.Info, file, $"the header counts {count} entries and the file holds {entries.Count}; every entry is kept (the header is only bumped, FRMGLDL reads the records)");
            foreach (var e in entries.Where(e => e.Guild < 0 || e.Guild >= GuildData.All.Count).Take(1))
                Note(NoteKind.Warning, file, $"an entry is filed under guild {e.Guild}, which MDATA1 has not");
        }
        GuildLog = entries;
        Files.Add(new ImportFile(file, "the guild log", $"read, {entries.Count} entries", entries.Count));
        return entries.Count;
    }

    // ------------------------------------------------------------------ MDATA10, the rooms

    private int ReadRooms(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.RoomsLen)!;
        int levels = f.Integer(1);
        if (levels != 15) Note(NoteKind.Warning, file, $"record 1 says {levels} levels where sub04A3 writes 15 (mc0118); 15 are read");
        var state = new DungeonState();
        int rooms = 0;
        for (int level = 1; level <= 15; level++)
        {
            // fn0DDC (MONSMOD.BAS:2213): the level's respawn tally at record 1 + level
            if (f.Has(1 + level)) state.Tally[level] = f.Integer(1 + level);
            for (int area = 1; area <= 200; area++)
            {
                // fn0E38 (MONSMOD.BAS:2380-2382): area a's Foes(0 to 3) and Fight at
                // ((a - 1) * 5 + slot) + (15 + 2) + (level - 1) * 200 * 5
                int first = (area - 1) * 5 + 17 + (level - 1) * 1000;
                if (!f.Has(first)) break;
                var foes = Enumerable.Range(0, 4).Select(s => f.Record(first + s, VbLayout.Foe)).ToArray();
                var fight = f.Record(first + 4, VbLayout.Fight);
                if (foes.All(x => x.IsZero) && fight.IsZero) continue;
                var room = new RoomRecord
                {
                    Foes = foes.Select(x => new FoeSlot
                    {
                        Att = x.Integer("Att"), Def = x.Integer("Def"), Hits = x.Integer("Hits"), MaxHits = x.Integer("MaxHits"),
                        Alignment = x.Integer("Alignment"), Engaged = x.Integer("Engaged"), Ref = x.Integer("Ref"),
                        GroupCount = x.Integer("GroupCount"), Stamp = x.Single("M4EE6"), IdLevel = x.Integer("IdLevel"),
                        JoinOffer = x.Integer("M4EF9"), KnownAs = x.Integer("KnownAs"),
                    }).ToArray(),
                    TreasureClass = fight.Integer("TreasureClass"),
                    Lead = fight.Integer("M4F24"),
                    Gold = fight.Currency("Gold").ToDecimal(),
                    ChestTrap = fight.Integer("ChestTrap"),
                    ChestLock = fight.Integer("ChestLock"),
                };
                foreach (var fs in room.Foes)
                    if (fs.GroupCount > 0 && (fs.Ref < 0 || fs.Ref >= _monsters.Count))
                        Note(NoteKind.Warning, file, $"level {level} area {area}: a standing group of monster table index {fs.Ref}, past MDATA5's {_monsters.Count}");
                state.StoreRoom(level, area, room);
                rooms++;
            }
        }
        Dungeon = state;
        Files.Add(new ImportFile(file, "the dungeon's rooms and respawn tallies", $"read, {rooms} rooms with records", f.RecordCount));
        return f.RecordCount;
    }

    // ------------------------------------------------------------------ MDATA12, the parties

    private int ReadParties(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.PartiesLen)!;
        if (f.Length == 0) { Files.Add(new ImportFile(file, "the remembered parties", "read, empty (no party remembered)", 0)); return 0; }
        // fn179B (MULTIMOD.BAS:387-444): record 1 a count, then four names a party from record 2,
        // the leader's first. The count only ever grows - a party forgotten is blanked in place - so
        // every group in the file is read and the blank ones passed over.
        int count = f.Integer(1);
        int groups = (f.RecordCount - 1) / 4;
        for (int g = 0; g < groups; g++)
        {
            int rec = 2 + g * 4;
            string leader = f.VarString(rec);
            if (leader.Trim() == "") continue;
            var members = new List<string> { leader };
            for (int m = 1; m < 4; m++)
            {
                string who = f.VarString(rec + m);
                if (who.Trim() != "") members.Add(who);
            }
            Parties.Add(new ImportedParty(leader, members));
        }
        if (count != groups)
            Note(NoteKind.Info, file, $"the header counts {count} parties and the file holds {groups} groups of four; every named group is read");
        Files.Add(new ImportFile(file, "the remembered parties", $"read, {Parties.Count} parties", f.RecordCount));
        return f.RecordCount;
    }

    // ------------------------------------------------------------------ MDATA13, the Library

    private int ReadLibrary(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.LibraryLen)!;
        if (!StampOk(f, file, "the Library")) return f.RecordCount;
        int nMon = f.Integer(2), nItems = f.Integer(3);
        Note(NoteKind.Info, file, $"its counts of monsters and items found ({f.Integer(4)} and {f.Integer(5)}, records 4 and 5) have no place in the port's library.json: the port keeps no such counts");
        // sub0F9A (HALL.BAS:60-80): monster i at record 6 + i, item j at 6 + monsters + j
        bool monOk = nMon == _monsters.Count, itemOk = nItems == _items.Count;
        if (!monOk) Note(NoteKind.Warning, file, $"has {nMon} monster records and MDATA5 {_monsters.Count}; each is taken by its Id (mdrUpdateLibraryFile re-indexes the same way)");
        if (!itemOk) Note(NoteKind.Warning, file, $"has {nItems} item records and MDATA3 {_items.Count}; each is taken by its Id");
        var lib = new Librarian.Store();
        var monsterList = _monsters.ToList();
        for (int i = 0; i < nMon && f.Has(6 + i); i++)
        {
            var rec = f.Record(6 + i, VbLayout.Library);
            if (Empty(rec)) continue;
            int at = monOk ? i : monsterList.FindIndex(m => m.Id == rec.Integer("Id"));
            if (at < 0) { Note(NoteKind.Warning, file, $"monster record {i} has Id {rec.Integer("Id")}, which MDATA5 has not; dropped"); continue; }
            Fill(lib.EditMonster(at), rec);
        }
        for (int j = 0; j < nItems && f.Has(6 + nMon + j); j++)
        {
            var rec = f.Record(6 + nMon + j, VbLayout.Library);
            if (Empty(rec)) continue;
            short id = rec.Integer("Id");
            if (id == 0 && itemOk && j < _items.Count) id = _items[j].Id;
            if (!_itemById.ContainsKey(id)) { Note(NoteKind.Warning, file, $"item record {j} has Id {id}, which MDATA3 has not; dropped"); continue; }
            Fill(lib.EditItem(id), rec);
        }
        Library = lib;
        return f.RecordCount;

        static bool Empty(VbRecord r) =>
            r.Integer("IdLevel") == 0 && r.Currency("M5C00").Raw == 0 && VbFile.RTrim(r.FixedString("M5C09")) == ""
            && r.Long("M5C21") == 0 && VbFile.RTrim(r.FixedString("M5C2F")) == "" && r.Integer("M5C3D") == 0;

        static void Fill(LibraryRecord e, VbRecord r)
        {
            e.Id = r.Integer("Id");
            e.Known = r.Integer("IdLevel");
            e.NSeen = r.Currency("M5C00").RoundToWhole();
            e.Name = VbFile.RTrim(r.FixedString("M5C09"));
            e.MonId = r.Integer("M5C14");
            e.LastSeen = r.Long("M5C21");
            e.Location = VbFile.RTrim(r.FixedString("M5C2F"));
            e.NDeaths = r.Integer("M5C3D");
        }
    }

    // ------------------------------------------------------------------ MDATA14, the Hall

    private int ReadHall(string path, string file)
    {
        var f = VbFile.Open(path, VbLayout.HallLen)!;
        // no version here: sub0595 writes record 1 as the stamp and then the first record over it
        // (MAINMOD.BAS:607-610, its m0164 a Static never set), and fn1C07 reads record k + 1 for
        // record k (HALL.BAS:617)
        var hall = new HallRecords();
        foreach (HallRecords.Kind k in Enum.GetValues<HallRecords.Kind>())
        {
            int rec = (int)k + 1;
            if (!f.Has(rec)) { Note(NoteKind.Warning, file, $"has no record for {HallRecords.Heading(k)}; the port's own first holder stands"); continue; }
            var r = f.Record(rec, VbLayout.Hall);
            hall.Records[(int)k] = new HallRecords.Record
            {
                Value = r.Currency("M73F5"),
                Holder = VbFile.RTrim(r.FixedString("M73FE")),
                Day = r.Long("M7406"),
            };
        }
        Hall = hall;
        return f.RecordCount;
    }
}
