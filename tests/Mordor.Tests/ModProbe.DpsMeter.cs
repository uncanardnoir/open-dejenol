using System.Reflection;
using System.Text.RegularExpressions;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// The DPS Meter mod and the hook under it (docs/MODDING.md): the combat engine's DamageDealt
/// (ModEvents.DamageDealt) raised for every hit point a character, or a companion of theirs, takes
/// off a monster and for every kill - checked against the monsters' own hits, the characters' Kills
/// and the captions - and nothing changed in a fight by listening; then the tallies (dpsmeter.json:
/// what each event adds, the round trip, Reset, a deleted character, a garbled file) and the mod
/// loaded through the host.
/// </summary>
public static partial class ModProbe
{
    /// <summary>A Character.Create character in one guild at a level, with hits to spare.</summary>
    private static Character Fighter(string name, string guild, int level)
    {
        var c = Character.Create(name, RaceData.All[0], Alignment.Neutral, Sex.Male, new[] { 18, 18, 18, 18, 18, 18 });
        c.Guilds.Clear();
        c.Guilds.Add(new GuildMembership { Guild = guild, Level = level });
        c.ClearPack();
        c.RecalcSwings();
        c.MaxHits = c.CurHits = 1_000_000;
        c.RecalcSpellPoints(refill: true);
        c.MaxSpellPoints = c.CurSpellPoints = 1_000_000;
        return c;
    }

    /// <summary>The hits taken off these monsters since <paramref name="before"/>: what each had less what it has, not below nought.</summary>
    private static long Removed(IReadOnlyList<CombatMonster> ms, IReadOnlyList<int> before) =>
        ms.Select((m, i) => (long)before[i] - Math.Max(0, m.Hits)).Where(x => x > 0).Sum();

    private static void DpsChecks(string dir, string scratch, string bundled, List<Item> items, Func<short, Item?> lookup)
    {
        var spells = MdrLoader.LoadSpells(Path.Combine(dir, "MDATA2.MDR"));
        var monsters = MdrLoader.LoadMonsters(Path.Combine(dir, "MDATA5.MDR"));
        string dpsDir = Path.Combine(scratch, "dps");
        Directory.CreateDirectory(dpsDir);

        // ---- the manifest, and nothing with the mod off ----
        Console.WriteLine("DPS Meter: the manifest, and mods off:");
        var log = new List<string>();
        var off = Host(Path.Combine(dpsDir, "off.ini"), log, bundled);
        var dm = off.Discovered.FirstOrDefault(d => d.Id == "DpsMeter");
        Check("DpsMeter manifest found, no problem", dm != null && dm.Problem == null, true);
        Check("its name and version (1.1.0: the XP mode, API version 8)", $"{dm?.Manifest.Name} {dm?.Manifest.Version}", "DPS Meter 1.1.0");
        Check("no toggles", dm?.Manifest.Settings.Count, 0);
        Check("not enabled by default", off.IsEnabled("DpsMeter"), false);
        var nobody = Fighter("Nobody", "Warrior", 1);
        off.DamageDealt(new CombatDamage(nobody, 5, DamageKind.Physical, 0, true));
        ModHost.Empty().DamageDealt(new CombatDamage(nobody, 5, DamageKind.Physical, 0, true));
        Check("mods off: a blow told to nobody, no dpsmeter.json written", File.Exists(Path.Combine(dpsDir, "dpsmeter.json")), false);

        var plain = monsters.Where(m => m.Id != 0 && m.Hits >= 15 && m.Hits <= 120 && m.LevelFound <= 4
                                     && !MonsterProperty.Has(m, MonsterProperty.NeedsGoodWeapon)
                                     && !MonsterProperty.Has(m, MonsterProperty.ImmuneToWeapons)
                                     && !MonsterProperty.Has(m, MonsterProperty.Invisible)
                                     && !MonsterProperty.Has(m, 2)).ToList();
        var sword = items.Where(i => i.Id != 0 && i.Type <= 7 && i.Att > 0 && i.Swings > 0).OrderBy(i => i.Floor).First();
        List<CombatMonster> Foes(int seed, int n, int tough = 1)
        {
            var r = new Random(seed);
            // two kinds, so a group spell reaches more than one group
            var a = plain[r.Next(plain.Count)];
            var b = plain[r.Next(plain.Count)];
            return Enumerable.Range(0, n).Select(i => new CombatMonster { Def = i < n / 2 ? a : b, Hits = (i < n / 2 ? a : b).Hits * tough }).ToList();
        }

        // ---- listening changes nothing ----
        Console.WriteLine("DPS Meter: the engine's DamageDealt changes nothing in a fight:");
        string Fight(bool listen, int seed)
        {
            var war = Fighter("War", "Warrior", 40);
            war.PutInPack(new InventorySlot { ItemId = sword.Id, Equipped = true, Identified = true });
            var mag = Fighter("Mag", "Mage", 30);
            var mageSpell = Spellbook.Known(mag, spells).Where(Spellbook.IsOffensive).OrderByDescending(s => s.Damage2).First();
            var owner = Fighter("Own", "Nomad", 10);
            foreach (var def in plain.Take(2)) owner.Companions.Add(new Companion { MonsterId = def.Id, Hits = 10_000, MaxHits = 10_000, Name = "" });
            var party = new Party(war, mag, owner);
            var foes = Foes(seed, 8);
            var fight = new CombatEngine(party, foes, lookup, new Random(seed));
            fight.UseMonsterTable(monsters);
            if (listen) { fight.DamageDealt = _ => { }; fight.ExperienceGained = _ => { }; }
            party.OrderFor(war).Kind = OrderKind.Fight;
            party.OrderFor(mag).Kind = OrderKind.Spell; party.OrderFor(mag).SpellId = mageSpell.Id;
            var said = new List<string>();
            for (int r = 0; r < 12 && !fight.IsOver; r++) said.AddRange(fight.RoundPaced(spells).Select(l => l.Text));
            return string.Join("|", said) + "#" + string.Join(",", foes.Select(f => f.Hits))
                 + "#" + string.Join(",", party.Members.Select(m => $"{m.CurHits}/{m.Kills}/{m.Guild.Experience}"));
        }
        int same = Enumerable.Range(0, 20).Count(s => Fight(false, 500 + s) == Fight(true, 500 + s));
        Check("20 fights (blows, spells, companions) the same with and without a listener", same, 20);

        // ---- the Messages lines' kinds (MessageKind, for QoL's Color-coded combat messages) ----
        Console.WriteLine("Messages lines of a fight carry the kind the engine gave them:");
        {
            int foesLines = 0, kindOffMessages = 0, partyNotMember = 0, foeLines = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var war = Fighter("War", "Warrior", 3);
                var mag = Fighter("Mag", "Mage", 3);
                var party = new Party(war, mag);
                var fight = new CombatEngine(party, Foes(9000 + seed, 8), lookup, new Random(seed));
                fight.UseMonsterTable(monsters);
                party.OrderFor(war).Kind = OrderKind.Fight;
                party.OrderFor(mag).Kind = OrderKind.Fight;
                for (int r = 0; r < 12 && !fight.IsOver; r++)
                    foreach (var l in fight.RoundPaced(spells))
                    {
                        if (l.Kind != MessageKind.None && l.Channel != LineChannel.Message) kindOffMessages++;
                        if (l.Kind == MessageKind.Foes) foesLines++;
                        if (l.Kind == MessageKind.Party && (l.Actor == null || !party.Members.Contains(l.Actor))) partyNotMember++;
                        if (l.Channel == LineChannel.Message && (l.Text.Contains("was killed by") || l.Text.Contains("Ouch!") || l.Text.Contains("Aigh!")) && l.Kind != MessageKind.Foes) foeLines++;
                    }
            }
            Check("weak members against eight foes: foes' lines marked Foes", foesLines > 0, true);
            Check("a kind only on Messages lines (captions and the log carry none)", kindOffMessages, 0);
            Check("a Party line is always a member's", partyNotMember, 0);
            Check("every 'killed by', 'Ouch!' and 'Aigh!' line is a foe's", foeLines, 0);
            Check("the kind changes no text: a Messages line made with it reads the same", new CombatEngine.CombatLine("x", Channel: LineChannel.Message, Kind: MessageKind.Foes).Text, "x");
            var lines = new List<string>(); var looks = new List<MessageLook>();
            for (int i = 1; i <= 25; i++) MessageList.Add(lines, looks, "line " + i, new MessageLook(i % 2 == 0 ? MessageKind.Foes : MessageKind.None, i % 2 == 0 ? ((byte)1, (byte)2, (byte)3) : null));
            Check("MessageList with looks: trimmed together, each line keeping its own",
                  $"{lines.Count} {looks.Count} {lines[0]} {looks[0].Kind} {looks[0].Colour} {looks[1].Kind} {looks[1].Colour?.ToString() ?? "-"}", "20 20 line 6 Foes (1, 2, 3) None -");
            lines.Insert(0, "out of step"); MessageList.Add(lines, looks, "line 26", new MessageLook(MessageKind.Party));
            Check("... lists out of step: every earlier look dropped (plain), none shifted onto another line",
                  $"{lines.Count} {looks.Count} {looks.Take(19).All(l => l == MessageLook.Plain)} {looks[^1].Kind}", "20 20 True Party");
        }

        // ---- melee: every swing, two members, the watched one and not ----
        Console.WriteLine("DPS Meter: blows (each swing, several swings, criticals and backstabs, a member not watched):");
        var hitLine = new Regex(@"for (\d+) damage!$");
        var killLines = new HashSet<string> { "You Killed One!", "You Severed one!", "You Backstabbed one!", "You Critically hit one!" };
        int badLines = 0, hitLines = 0, killLinesSeen = 0, missLines = 0, multiKillRounds = 0, wrongShown = 0, wrongSum = 0, wrongKills = 0, wrongKind = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            var ninja = Fighter("Nin", "Ninja", 80);
            ninja.PutInPack(new InventorySlot { ItemId = sword.Id, Equipped = true, Identified = true });
            var other = Fighter("Oth", "Warrior", 25);
            other.PutInPack(new InventorySlot { ItemId = sword.Id, Equipped = true, Identified = true });
            var party = new Party(ninja, other);
            var foes = Foes(1000 + seed, 6);
            var before = foes.Select(f => f.Hits).ToList();
            var fight = new CombatEngine(party, foes, lookup, new Random(seed));
            var got = new List<CombatDamage>();
            var all = new List<CombatDamage>();
            fight.DamageDealt = d => { got.Add(d); all.Add(d); };
            foreach (var who in new[] { ninja, other })
            {
                int kills = 0;
                foreach (var line in fight.PlayerSwings(who))
                {
                    var mine = got.ToList(); got.Clear();
                    if (mine.Any(d => d.Character != who || d.Kind != DamageKind.Physical)) wrongKind++;
                    if (mine.Any(d => d.Shown != (who == party.Selected))) wrongShown++;
                    if (hitLine.Match(line.Text) is { Success: true } m)
                    {
                        hitLines++;
                        if (mine.Count != 1 || mine[0].Amount != int.Parse(m.Groups[1].Value) || mine[0].Kills != 0) badLines++;
                    }
                    else if (killLines.Contains(line.Text))
                    {
                        killLinesSeen++; kills++;
                        // the hits the monster had left, then the kill
                        if (mine.Count == 0 || mine[^1].Kills != 1 || mine[^1].Amount != 0 || mine.Take(mine.Count - 1).Any(d => d.Kills != 0 || d.Amount <= 0)) badLines++;
                    }
                    else { missLines++; if (mine.Count != 0) badLines++; }
                }
                if (kills > 1) multiKillRounds++;
            }
            if (all.Sum(d => (long)d.Amount) != Removed(foes, before)) wrongSum++;
            if (all.Where(d => d.Character == ninja).Sum(d => d.Kills) != ninja.Kills || all.Where(d => d.Character == other).Sum(d => d.Kills) != other.Kills) wrongKills++;
        }
        Check("swings seen: hits, kills, misses (each kind at least once)", hitLines > 0 && killLinesSeen > 0 && missLines > 0, true);
        Check("a round with more than one kill (the extra swings)", multiKillRounds > 0, true);
        Check("a hit raises one event, the N of 'for N damage!'; a miss none; a kill its hits then the kill", badLines, 0);
        Check("every blow the swinger's, Physical", wrongKind, 0);
        Check("Shown: only the one in command's", wrongShown, 0);
        Check("the amounts add up to the hits the monsters lost", wrongSum, 0);
        Check("the kills are each one's Kills", wrongKills, 0);

        // ---- spells: single and group, a character's own and an item's ----
        Console.WriteLine("DPS Meter: spells (single, group, an item's):");
        var mage = Fighter("Mag", "Sorcerer", 60);
        var offensive = Spellbook.Known(mage, spells).Where(Spellbook.IsOffensive)
            .Where(s => s.Effect != 100 && s.Effect is < 75 or > 99 && s.Category != Spellbook.Charm && s.Damage2 > 0).ToList();
        var single = offensive.Where(s => !Spellbook.IsGroup(s)).OrderByDescending(s => s.Damage2).FirstOrDefault();
        var group = offensive.Where(Spellbook.IsGroup).OrderByDescending(s => s.Damage2).FirstOrDefault();
        Check("a single and a group damage spell to cast", single != null && group != null, true);
        var damaged = new Regex(@"damaged one for (\d+)");
        int casts = 0, spellBad = 0, spellSum = 0, spellKills = 0, spellKind = 0, captionsMatched = 0, withKills = 0;
        for (int seed = 0; seed < 200 && single != null && group != null; seed++)
        {
            var caster = Fighter("Cas", "Sorcerer", 60);
            var watcher = Fighter("Wat", "Warrior", 5);
            var party = new Party(watcher, caster);   // the caster not in command
            if (seed % 2 == 0) party.Select(1);
            var foes = Foes(3000 + seed, 8, seed % 4 < 2 ? 1 : 6);
            var fight = new CombatEngine(party, foes, lookup, new Random(seed));
            var got = new List<CombatDamage>();
            fight.DamageDealt = got.Add;
            for (int k = 0; k < 3 && !fight.IsOver; k++)
            {
                var before = foes.Select(f => f.Hits).ToList();
                long killsBefore = caster.Kills;
                var s = (seed + k) % 3 == 0 ? single : group;
                bool item = k == 2;
                // an item's cast is the same CastSpell with the item's level (ItemOrderActs)
                fight.CastSpell(caster, s, null, free: item, itemLevel: item ? 10 : 0);
                casts++;
                if (got.Any(d => d.Character != caster || d.Kind != DamageKind.Magical || d.Shown != (party.Selected == caster))) spellKind++;
                if (got.Sum(d => (long)d.Amount) != Removed(foes, before)) spellSum++;
                if (got.Sum(d => d.Kills) != caster.Kills - killsBefore) spellKills++;
                if (got.Any(d => d.Kills > 0)) withKills++;
                // a caption's "damaged one for N" is the hits that survivor lost: one event of N
                foreach (var cap in fight.LastCaptions)
                    if (damaged.Match(cap) is { Success: true } m)
                    {
                        if (got.Any(d => d.Amount == int.Parse(m.Groups[1].Value) && d.Kills == 0)) captionsMatched++;
                        else spellBad++;
                    }
                got.Clear();
            }
        }
        Check("casts made, some killing", casts > 0 && withKills > 0, true);
        Check("every spell event the caster's, Magical, Shown only in command", spellKind, 0);
        Check("the amounts add up to the hits the monsters lost, each cast", spellSum, 0);
        Check("the kills are the caster's Kills, each cast", spellKills, 0);
        Check("survivors' captions seen ('damaged one for N')", captionsMatched + spellBad > 0, true);
        Check("each 'damaged one for N' told as N", $"{spellBad} (of {captionsMatched + spellBad})", $"0 (of {captionsMatched})");

        // ---- a charm: the kill, as the game counts it, and no damage (the creature walks off alive) ----
        Console.WriteLine("DPS Meter: a charm (its kill, no damage):");
        var orc = monsters.First(m => m.Name == "Orc");
        int charmed = 0, charmDamage = 0, charmKills = 0, charmNoXp = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var charmer = Fighter("Chm", "Mage", 20);
            var charm = Spellbook.Known(charmer, spells).First(s => s.Name == "Charm Humanoid");
            var party = new Party(charmer);
            var foes = Enumerable.Range(0, 2).Select(_ => new CombatMonster { Def = orc, Hits = orc.Hits }).ToList();
            var fight = new CombatEngine(party, foes, lookup, new Random(seed));
            fight.UseMonsterTable(monsters);
            var got = new List<CombatDamage>();
            fight.DamageDealt = got.Add;
            long xpBefore = charmer.Guild.Experience;
            fight.CastSpell(charmer, charm);
            if (charmer.Kills == 0) continue;
            charmed++;
            if (got.Any(d => d.Amount != 0 || d.Kind != DamageKind.Magical)) charmDamage++;
            if (got.Sum(d => d.Kills) != charmer.Kills) charmKills++;
            // the game itself is unchanged: experience is still paid on the charmed creature's hits
            if (charmer.Guild.Experience <= xpBefore) charmNoXp++;
        }
        Check("charms that took a creature", charmed > 0, true);
        Check("a charm raises no damage", charmDamage, 0);
        Check("a charm raises its kills, as the caster's Kills count them", charmKills, 0);
        Check("and the game still pays experience for it", charmNoXp, 0);

        // ---- companions: the owner's, a kind of their own ----
        Console.WriteLine("DPS Meter: companions (breath, spit, blows, spells) credited to the owner:");
        Monster? Pick(Func<Monster, bool> f) => monsters.Where(m => m.Id != 0 && m.Att > 0 && m.GuildLevel <= 60).FirstOrDefault(f);
        var comps = new[]
        {
            Pick(m => (m.SpecialAttackFlags & (1 << 3)) != 0),   // breathes fire
            Pick(m => (m.SpecialAttackFlags & (1 << 5)) != 0),   // spits
            Pick(m => m.SpellFlags > 0),                         // casts
            Pick(m => m.SpecialAttackFlags == 0 && m.SpellFlags == 0),
        }.Where(m => m != null).Select(m => m!).ToList();
        Check("companions to fight with (breather, spitter, caster, striker)", comps.Count, 4);
        int compEvents = 0, compWrong = 0, compSum = 0, compKillsBad = 0, compKillsSeen = 0, mixedKinds = 0;
        for (int seed = 0; seed < 150; seed++)
        {
            var owner = Fighter("Own", "Warrior", 30);
            owner.PutInPack(new InventorySlot { ItemId = sword.Id, Equipped = true, Identified = true });
            var mate = Fighter("Mat", "Warrior", 1);
            foreach (var def in comps) owner.Companions.Add(new Companion { MonsterId = def.Id, Hits = 100_000, MaxHits = 100_000, Name = "" });
            var party = new Party(mate, owner);
            party.OrderFor(mate).Kind = OrderKind.Defend;
            party.OrderFor(owner).Kind = seed % 2 == 0 ? OrderKind.Fight : OrderKind.Defend;
            var foes = Foes(5000 + seed, 8);
            var before = foes.Select(f => f.Hits).ToList();
            var fight = new CombatEngine(party, foes, lookup, new Random(seed));
            fight.UseMonsterTable(monsters);
            var got = new List<CombatDamage>();
            fight.DamageDealt = got.Add;
            for (int r = 0; r < 6 && !fight.IsOver; r++) foreach (var _ in fight.RoundPaced(spells)) { }
            compEvents += got.Count(d => d.Kind == DamageKind.Companion);
            if (got.Any(d => d.Character != owner)) compWrong++;
            if (got.Any(d => d.Kind == DamageKind.Magical)) compWrong++;
            if (party.OrderFor(owner).Kind == OrderKind.Defend && seed % 2 == 1 && got.Any(d => d.Kind == DamageKind.Physical)) compWrong++;
            if (got.Select(d => d.Kind).Distinct().Count() > 1) mixedKinds++;
            if (got.Sum(d => (long)d.Amount) != Removed(foes, before)) compSum++;
            // every monster that fell, by the owner or a companion (no charms here)
            int fallen = foes.Count(f => !f.Alive && !f.Gone);
            int ownKills = got.Where(d => d.Kind == DamageKind.Physical).Sum(d => d.Kills);
            compKillsSeen += got.Where(d => d.Kind == DamageKind.Companion).Sum(d => d.Kills);
            if (got.Sum(d => d.Kills) != fallen || ownKills != owner.Kills) compKillsBad++;
        }
        Check("companion events raised, some kills among them", compEvents > 0 && compKillsSeen > 0, true);
        Check("all of it the owner's, Physical or Companion only", compWrong, 0);
        Check("the owner's blows and the companions' in one fight", mixedKinds > 0, true);
        Check("the amounts add up to the hits the monsters lost", compSum, 0);
        Check("kills: every monster that fell, the owner's own on their Kills", compKillsBad, 0);

        // ---- the tallies (dpsmeter.json) ----
        Console.WriteLine("DPS Meter: the tallies:");
        var dpsAsm = Assembly.LoadFrom(Path.Combine(bundled, "DpsMeter", "Mordor.Mod.DpsMeter.dll"));
        var tallyType = dpsAsm.GetType("Mordor.Mod.DpsMeter.DpsTally", throwOnError: true)!;
        var ctor = tallyType.GetConstructors().Single();
        dynamic NewTally(string? path, TimeSpan? delay = null) => ctor.Invoke(new object?[] { path, null, delay });
        string Row(dynamic t, string name) { var r = t.For(name); return $"{r.Physical}/{r.Magical}/{r.Companion}/{r.Total}/{r.Kills}"; }
        var a = Fighter("Anna", "Warrior", 1);
        var b = Fighter("Bert", "Warrior", 1);
        string file = Path.Combine(dpsDir, "tally", "dpsmeter.json");
        var t1 = NewTally(file, TimeSpan.FromMilliseconds(300));
        t1.Add(new CombatDamage(a, 12, DamageKind.Physical, 0, true));
        t1.Add(new CombatDamage(a, 0, DamageKind.Physical, 1, true));
        t1.Add(new CombatDamage(a, 30, DamageKind.Magical, 0, false));
        t1.Add(new CombatDamage(a, 7, DamageKind.Companion, 0, false));
        t1.Add(new CombatDamage(a, 0, DamageKind.Companion, 1, false));
        t1.Add(new CombatDamage(b, 5, DamageKind.Physical, 0, false));
        Check("each kind in its column, kills (the companion's too) in Kills, shown or not", Row(t1, "Anna"), "12/30/7/49/2");
        Check("names matched without case or spaces", Row(t1, " anna "), "12/30/7/49/2");
        // a burst: nothing written while it lasts, then one write once it is quiet
        for (int i = 0; i < 200; i++) t1.Add(new CombatDamage(b, 1, DamageKind.Companion, i % 10 == 0 ? 1 : 0, false));
        Check("a burst of 206 events: nothing written while it lasts", $"{File.Exists(file)} {t1.Writes} {t1.Dirty}", "False 0 True");
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (t1.Writes == 0 && waited.ElapsedMilliseconds < 5000) Thread.Sleep(20);
        Thread.Sleep(400);   // and no second write after it
        Check("then one write, once it is quiet", $"{File.Exists(file)} {t1.Writes} {t1.Dirty}", "True 1 False");
        var t2 = NewTally(file);
        Check("read back: Anna", Row(t2, "Anna"), "12/30/7/49/2");
        Check("read back: Bert, the whole burst", Row(t2, "Bert"), "5/0/200/205/20");
        // closing (the program exiting) writes what is still waiting, at once
        var t4 = NewTally(file, TimeSpan.FromMinutes(10));
        t4.Add(new CombatDamage(a, 1000, DamageKind.Magical, 3, true));
        Check("a change waiting on a long delay: not yet written", Row(NewTally(file), "Anna"), "12/30/7/49/2");
        t4.Dispose();
        Check("closed: written, nothing lost", $"{Row(NewTally(file), "Anna")} {t4.Writes}", "12/1030/7/1049/5 1");
        t2 = NewTally(file);
        t2.Remove("Bert");
        Check("a deleted character dropped, and the file follows at once", Row(NewTally(file), "Bert"), "0/0/0/0/0");
        t2.Reset();
        Check("Reset: nothing left", Row(t2, "Anna"), "0/0/0/0/0");
        Check("Reset: the file empty too, at once", Row(NewTally(file), "Anna") + " " + ((IEnumerable<string>)NewTally(file).Characters).Count(), "0/0/0/0/0 0");
        File.WriteAllText(file, "{ not json");
        var t3 = NewTally(file);
        Check("a garbled file: the tallies start empty", ((IEnumerable<string>)t3.Characters).Count(), 0);
        Check("and it is moved aside, not written over", t3.MovedAside != null && File.Exists((string)t3.MovedAside) && !File.Exists(file), true);

        // ---- the experience (ModEvents.ExperienceGained, API version 8): sub0F11's every award ----
        Console.WriteLine("DPS Meter: experience gained (blows, spells, companions, a chest, the pin):");
        {
            int seqBad = 0, sumBad = 0, sourceBad = 0, xpEvents = 0;
            var kindsSeen = new HashSet<DamageKind>();
            for (int seed = 0; seed < 40; seed++)
            {
                var war = Fighter("War", "Warrior", 40);
                war.PutInPack(new InventorySlot { ItemId = sword.Id, Equipped = true, Identified = true });
                var mag = Fighter("Mag", "Sorcerer", 60);
                var owner = Fighter("Own", "Nomad", 10);
                foreach (var def in plain.Take(2)) owner.Companions.Add(new Companion { MonsterId = def.Id, Hits = 10_000, MaxHits = 10_000, Name = "" });
                var party = new Party(war, mag, owner);
                var before = party.Members.ToDictionary(m => m, m => m.Guild.Experience);
                var fight = new CombatEngine(party, Foes(7000 + seed, 8), lookup, new Random(seed));
                fight.UseMonsterTable(monsters);
                // the order both arrive in: a damage event, then at once the award it earned
                var seq = new List<object>();
                fight.DamageDealt = d => seq.Add(d);
                fight.ExperienceGained = x => seq.Add(x);
                party.OrderFor(war).Kind = OrderKind.Fight;
                // a damage spell cast first, as the spells above are (a single or a group one)
                if (single != null && group != null) fight.CastSpell(mag, seed % 2 == 0 ? single : group, null);
                for (int r = 0; r < 12 && !fight.IsOver; r++) fight.RoundPaced(spells).ToList();
                for (int i = 0; i < seq.Count; i++)
                {
                    if (seq[i] is CombatExperience x) { xpEvents++; if (x.Source != ExperienceSource.Damage) sourceBad++; continue; }
                    var d = (CombatDamage)seq[i];
                    if (d.Kills != 0) continue;
                    kindsSeen.Add(d.Kind);
                    if (i + 1 >= seq.Count || seq[i + 1] is not CombatExperience next || next.Character != d.Character) seqBad++;
                }
                foreach (var m in party.Members)
                    if (seq.OfType<CombatExperience>().Where(x => x.Character == m).Sum(x => x.Amount) != m.Guild.Experience - before[m]) sumBad++;
            }
            Check("every blow's, spell's and companion's damage is followed at once by its award, to the same character (40 fights)",
                  $"{seqBad} {string.Join(",", kindsSeen.OrderBy(k => k))}", "0 Physical,Magical,Companion");
            Check("the awards add up to each member's experience gained, a companion's to its owner", sumBad, 0);
            Check("and every one of them is of damage", (sourceBad, xpEvents > 0), (0, true));

            // a chest's trap disarmed, and the pin's clamp: what the total went up by
            var p = Fighter("Pin", "Warrior", 3);
            var solo = new Party(p);
            var engine = new CombatEngine(solo, Foes(1, 2), lookup, new Random(1));
            var got = new List<CombatExperience>();
            engine.ExperienceGained = got.Add;
            p.Guild.Experience = 0;
            engine.GiveExperience(p, 75, ExperienceSource.Chest);
            Check("a chest's disarm: one event, its amount, of Chests", string.Join(";", got.Select(x => $"{x.Character.Name} {x.Amount} {x.Source}")), "Pin 75 Chest");
            got.Clear();
            p.Guild.Experience = p.ExperienceCap() - 10;
            engine.GiveExperience(p, 100);
            Check("an award the pin clamps: only the 10 added", string.Join(";", got.Select(x => $"{x.Amount} {x.Source}")) + " " + (p.Guild.Experience == p.ExperienceCap()), "10 Damage True");
            got.Clear();
            engine.GiveExperience(p, 100);
            Check("pinned already: still one event, of nothing added", string.Join(";", got.Select(x => $"{x.Amount}")), "0");
            got.Clear();
            p.Guild.Experience = p.ExperienceCap() + 500;
            engine.GiveExperience(p, 100);
            Check("past a lowered pin, pulled back to it: nothing added, never less", string.Join(";", got.Select(x => $"{x.Amount}")), "0");
            Check("the dungeon's disarm is the chest source, and wired for the mods",
                  PortSource("src/Mordor.App", "DungeonWindow.axaml.cs").Contains("if (xp > 0) fight.GiveExperience(opener, xp, ExperienceSource.Chest);")
                  && PortSource("src/Mordor.App", "DungeonWindow.axaml.cs").Contains("_combat.ExperienceGained = x => Mordor.Modding.ModHost.Active.ExperienceGained(x);"), true);
            Check("sub0F11 has only the two callers (Credit and the disarm) besides itself",
                  Regex.Matches(PortSource("src/Mordor.Core", "Combat.cs") + PortSource("src/Mordor.App", "DungeonWindow.axaml.cs"), @"\bGiveExperience\(").Count, 3);
        }

        // the tallies keep the experience, and a file from before it reads as nought
        {
            string xfile = Path.Combine(dpsDir, "xp", "dpsmeter.json");
            string Xp(dynamic t, string name) { var r = t.For(name); return $"{r.XpDamage}/{r.XpChests}/{r.XpTotal}"; }
            var tx = NewTally(xfile, TimeSpan.FromMinutes(10));
            tx.Add(new CombatDamage(a, 12, DamageKind.Physical, 0, true));
            tx.Add(new CombatExperience(a, 300, ExperienceSource.Damage));
            tx.Add(new CombatExperience(a, 40, ExperienceSource.Chest));
            tx.Add(new CombatExperience(b, 0, ExperienceSource.Damage));
            Check("experience by source: damage and chests", $"{Xp(tx, "Anna")} {Row(tx, "Anna")}", "300/40/340 12/0/0/12/0");
            tx.Dispose();
            Check("written and read back, with the damage", $"{Xp(NewTally(xfile), "Anna")} {Row(NewTally(xfile), "Anna")}", "300/40/340 12/0/0/12/0");
            Check("an award of nothing adds no character", ((IEnumerable<string>)NewTally(xfile).Characters).Count(), 1);
            File.WriteAllText(xfile, "{ \"Version\": 1, \"Characters\": { \"Cas\": { \"Physical\": 1520, \"Magical\": 340, \"Companion\": 75, \"Kills\": 12 } } }");
            var old = NewTally(xfile);
            Check("a file written before the experience was kept: its damage, and nought experience", $"{Row(old, "Cas")} {Xp(old, "Cas")}", "1520/340/75/1935/12 0/0/0");
            old.Add(new CombatExperience(Fighter("Cas", "Warrior", 1), 9, ExperienceSource.Chest));
            old.Reset();
            Check("Reset clears the experience with the damage", $"{Row(old, "Cas")} {Xp(old, "Cas")}", "0/0/0/0/0 0/0/0");
        }

        // ---- the mod through the host ----
        Console.WriteLine("DPS Meter: the mod loaded:");
        string onIni = Path.Combine(dpsDir, "on.ini");
        var store = new ModSettingsStore(onIni);
        store.SetEnabled("DpsMeter", true);
        var on = Host(onIni, log, bundled);
        var lm = on.Loaded.FirstOrDefault(l => l.Id == "DpsMeter");
        Check("loaded, not faulted", lm != null && !lm.Faulted, true);
        Check("its window offered", string.Join(",", on.Windows.Select(w => w.Window.Title)), "DPS Meter");
        Check("a save tagged with the earlier 1.0.0 is not told a mod is missing (the tag is matched by id)", on.MissingFrom(new[] { "DpsMeter@1.0.0" }).Count, 0);
        on.DamageDealt(new CombatDamage(a, 40, DamageKind.Physical, 0, true));
        on.DamageDealt(new CombatDamage(a, 0, DamageKind.Magical, 1, true));
        string json = Path.Combine(dpsDir, "dpsmeter.json");
        dynamic modTally = ((dynamic)lm!.Instance!).Tally;
        Check("the mod's tally waits a second before writing", $"{modTally.SaveDelay.TotalMilliseconds} {modTally.Dirty}", "1000 True");
        modTally.Flush();
        Check("an event through the host lands in dpsmeter.json beside settings.ini", Row(NewTally(json), "Anna"), "40/0/0/40/1");
        on.CharacterDeleted("Anna");
        Check("Delete Character drops the tally", Row(NewTally(json), "Anna"), "0/0/0/0/0");
    }
}
