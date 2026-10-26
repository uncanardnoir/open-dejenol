namespace Mordor.Core;

/// <summary>
/// The game's arithmetic, ported from the decompiled source (decompiled-named/*.BAS): each rule
/// cites the routine and lines it follows. Where a dejenol.com community page (docs/dejenol-formulas)
/// states the same rule it is named beside the source as a cross-check, never as the authority.
/// Rnd is [0,1).
/// </summary>
public static class Rules
{
    // ------------------------------------------------------------------ Attack / Defense

    /// <summary>
    /// Stats A/D, <c>mdrcharSetAD</c> (MAINMOD2.BAS:4455-4548) on Stats + StatMods: +2 per STR/DEX
    /// point above 12, +1 per INT/WIS above 14; flat in the 8..12 / 8..14 bands; the same weights
    /// off below 8. The Stats_A-D_Formula page agrees.
    /// </summary>
    public static int StatsAD(int[] s)
    {
        static int Term(int v, int lo, int hi, int w) => v > hi ? w * (v - hi) : v < lo ? w * (v - lo) : 0;
        return Term(s[(int)Stat.Str], 8, 12, 2) + Term(s[(int)Stat.Dex], 8, 12, 2) + Term(s[(int)Stat.Int], 8, 14, 1) + Term(s[(int)Stat.Wis], 8, 14, 1);
    }

    // Guild A/D by level (A-D_For_all_Guild_Levels): the five tables the community completed, "att/def:level".
    /// <summary>
    /// The community tables, kept only as a regression check on <see cref="GuildAD"/>, which is the
    /// source's rule: <c>Mordor.Tools adcheck</c> and selfcheck compare every value. Nothing in
    /// the game reads them.
    /// </summary>
    public static readonly Dictionary<string, string> GuildTables = new()
    {
        ["Nomad"] = "10/10:1 13/13:2 16/16:4 19/19:6 22/22:8 25/25:10 28/28:12 31/31:14 34/34:16 37/37:18 40/40:20 43/43:22 46/46:24 49/49:26 52/52:28 55/55:30 58/58:33 61/61:36 64/64:39 67/67:42 70/70:45 73/73:49 76/76:53 79/79:57 82/82:61 85/85:65 88/88:69 91/91:73 94/94:77 97/97:81 100/100:85 103/103:89 106/106:93 109/109:97 112/112:101 115/115:105 118/118:109 121/121:113 124/124:117 127/127:121 130/130:125 133/133:129 136/136:133 139/139:137 142/142:142 145/145:147 148/148:152 151/151:157 154/154:162 157/157:167 160/160:172 163/163:177 166/166:182 169/169:187 172/172:192 175/175:197",
        ["Warrior"] = "13/13:4 16/17:8 19/20:12 23/24:16 26/28:20 29/31:24 33/35:28 36/38:32 39/42:36 42/46:40 46/49:44 49/53:48 52/56:52 56/60:56 59/63:60 62/67:64 66/71:68 69/74:72 72/78:76 75/81:81 79/85:86 82/89:91 85/92:96 89/96:101 92/99:106 95/103:111 99/107:116 102/110:121 105/114:126 109/117:131 112/121:136 115/125:141 118/128:146 122/132:152 125/135:158 128/139:164 132/143:170 135/146:176 138/150:182 142/153:188 145/157:194 148/161:200 151/164:206 155/168:212 158/172:218 161/175:225 165/179:232 168/182:239 171/186:246 175/190:253 178/193:260 181/197:267 184/200:274 188/204:281 191/208:288 194/211:296 198/215:304 201/218:312 204/222:320 208/226:328 211/229:336 214/233:344 217/236:352 221/240:360 224/244:368 227/247:377 231/251:386 234/254:395 237/258:404 241/262:413 244/265:422 247/269:431 250/272:440 254/276:450",
        ["Paladin"] = "13/13:3 16/17:6 19/20:9 22/24:12 25/28:15 28/31:18 31/35:21 34/38:24 37/42:27 41/46:33 44/49:37 47/53:41 50/56:45 53/60:49 56/63:54 59/67:59 62/71:64 65/74:69 68/78:74 71/81:79 75/85:84 78/89:89 81/92:94 84/96:99 87/99:104 90/103:109 93/107:114 96/110:119 99/114:125 102/117:131 106/121:137 109/125:143 112/128:149 115/132:155 118/135:161 121/139:167 124/143:173 127/146:179 130/150:185 133/153:192 137/157:199 140/161:206 143/164:213 146/168:220 149/172:227 152/175:234 155/179:241 158/182:248 161/186:255 165/190:263 168/193:271 171/197:279 174/200:287 177/204:295 180/208:303 183/211:311 186/215:319 189/218:327 192/222:336 196/226:345 199/229:354 202/233:363 205/236:372 208/240:381 211/244:390 214/247:400 217/251:410 220/254:420 223/258:430",
        ["Seeker"] = "10/10:1 13/13:3 16/17:6 19/21:9 22/25:12 26/29:15 29/33:18 32/37:21 35/41:24 38/45:27 42/49:32 45/52:37 48/56:42 51/60:47 54/64:52 58/68:57 61/72:62 64/76:67 67/80:72 70/84:77 74/88:83 77/91:89 80/95:95 83/99:101 86/103:107 89/107:113 93/111:119 96/115:125 99/119:131 102/123:137 105/127:144 109/130:151 112/134:158 115/138:165 118/142:172 121/146:179 125/150:186 128/154:193 131/158:201 134/162:209 137/165:217 141/169:225 144/173:233 147/177:241 150/181:249 153/185:258 157/189:267 160/193:276 163/197:285 166/201:294 169/204:303 173/208:312 176/212:322 179/216:332 182/220:342 185/224:352 189/228:362 192/232:373 195/236:384 198/240:395 201/243:406 205/247:417 208/251:428",
        ["Thief"] = "12/12:3 14/15:6 16/18:9 18/20:12 21/23:15 23/26:18 25/28:21 27/31:24 29/34:27 32/37:30 34/39:33 36/42:37 38/45:41 40/47:45 43/50:49 45/53:53 47/55:57 49/58:61 51/61:65 54/64:69 56/66:73 58/69:77 60/72:81 62/74:85 65/77:89 67/80:93 69/82:97 71/85:102 73/88:107 76/90:112 78/93:117 80/96:122 82/99:127 84/101:132 87/104:137 89/107:142 91/109:147 93/112:152 95/115:157 97/117:162 100/120:168 102/123:174 104/126:180 106/128:186 108/131:192 111/134:198 113/136:204 115/139:210 117/142:216 119/144:222 122/147:229 124/150:236 126/153:243 128/155:250 130/158:257 133/161:264 135/163:271 137/166:278 139/169:285 141/171:293 144/174:301 146/177:309 148/180:317 150/182:325 152/185:333 155/188:341 157/190:349 159/193:358 161/196:367 163/198:376 166/201:385 168/204:394",
    };

    /// <summary>
    /// Every character begins at 10/10, set in CHARCREA.BAS (<c>mc00C6</c>, <c>mc00C8</c>). Guild
    /// awards and stat modifiers are added on top of this.
    /// </summary>
    public const int BaseAD = 10;

    /// <summary>
    /// How often a guild hands out A/D once past its <see cref="GuildDef.HitsCapLevel"/>. This is
    /// <c>fn13D4</c> in MAINMOD2.BAS - the interval stretches as the level climbs, and stretches
    /// again past the guild's A/D ceiling:
    /// <code>
    ///   acc = M559A : bonus = 0 : If guild is the first one (Nomad) Then bonus = 3
    ///   For i = M559A To level
    ///       acc = acc + (((Int(M559A) / 2) + (acc / 18)) / (M5626 + bonus))
    ///       If acc > level Then Exit For
    ///   Next
    ///   If acc = M559A Then acc = level + 1
    ///   If level > M561A Then acc = acc * 1.1
    /// </code>
    /// </summary>
    private static int AdInterval(GuildDef g, int level, bool isNomad)
    {
        float acc;
        if (level > g.HitsCapLevel)
        {
            acc = g.HitsCapLevel;
            double bonus = isNomad ? 3 : 0;
            for (int i = g.HitsCapLevel; i <= level; i++)
            {
                // l0434 is declared Integer, so the step is rounded before it is added - not
                // accumulated as a fraction. That one detail is what makes the published tables
                // come out right; without it every guild drifts within a few levels.
                int step = (int)Math.Round(((int)g.HitsCapLevel / 2.0 + acc / 18f) / (g.AdEvery + bonus),
                                           MidpointRounding.ToEven);
                acc += step;
                if (acc > level) break;
            }
            if (Math.Abs(acc - g.HitsCapLevel) < 1e-9) acc = level + 1;
        }
        else acc = 0;
        if (level > g.AdMaxLevel) acc = (float)(acc * 1.1);   // 1.1# is a Double literal in the source
        return (int)Math.Round(acc);
    }

    /// <summary>
    /// The attack and defence a guild has granted by a given level, read from MDATA1 rather than
    /// approximated. FRMCTYG.BAS awards <see cref="GuildDef.AttPerLevel"/> only on levels where
    /// <c>level Mod Int(AdEvery)</c> is zero, stops entirely past <see cref="GuildDef.AdMaxLevel"/>,
    /// and switches to the stretching interval above <see cref="GuildDef.HitsCapLevel"/>.
    /// Reproduces every value in the published tables - all 334 of them, across every level of the
    /// five guilds the community tabulated completely, above the cap as well as below. Nomad 13/13
    /// at 2, 16/16 at 4, 19/19 at 6, 25/25 at 10; Warrior 13/13 at 4 and 42/46 at 40; Thief 12/12
    /// at 3, 14/15 at 6, 16/18 at 9 - all with the 10/10 base. <c>Mordor.Tools adcheck</c> is
    /// that comparison and names any value that parts company.
    /// </summary>
    public static (int att, int def) GuildAD(string guild, int level)
    {
        var (fa, fd) = GuildADSingle(guild, level);
        return ((int)fa, (int)fd);
    }

    /// <summary>
    /// <c>GuildsJoined(g).Att</c> and <c>.Def</c> as the Singles they are, before the whole part is
    /// taken, which the Char page's guild list (MAINMOD2.BAS:5305) prints with <c>CStr</c>.
    /// </summary>
    public static (float att, float def) GuildADSingle(string guild, int level)
    {
        var g = GuildData.All.FirstOrDefault(x => x.Name == guild);
        if (g == null || g.AttPerLevel <= 0) return (0, 0);
        bool isNomad = GuildData.All.Count > 0 && GuildData.All[0].Name == guild;

        // This used to fall back to the community's published tables above the guild's cap,
        // because fn13D4's arithmetic did not line up. It does now - see AdInterval - so the same
        // data-driven rule runs at every level and the tables are only a regression check.
        float att = 0, def = 0;
        int cached = 0;                                   // the character's M59A1
        int every = Math.Max(1, (int)g.AdEvery);
        for (int l = 1; l <= level && l <= g.AdMaxLevel; l++)
        {
            int r;
            if (l <= g.HitsCapLevel) r = l % every;
            else
            {
                if (cached < l) cached = AdInterval(g, l, isNomad);
                r = cached <= 0 ? 0 : l % cached;
            }
            if (r == 0)
            {
                att += g.AttPerLevel; def += g.DefPerLevel;
                if (l >= g.HitsCapLevel) cached = AdInterval(g, l, isNomad);
            }
            cached = AdInterval(g, l, isNomad);           // recomputed every level, as FRMCTYG does
        }
        // Single, and truncated - and it has to be both. `GuildsJoined(g).Att` is a Single that
        // modSetNewAD adds `GuildDefs(g).Att` to on every award (FRMCTYG.BAS:1019), and the figure
        // on screen is the whole part of it.
        //
        // A Warrior gains 3.3 attack and 3.6 defence an award. Ten awards is 43.0 / 46.0 in exact
        // arithmetic, and the published table says 42/46 at level 40. In Single precision 3.3 is
        // 3.29999995..., so the tenth award lands on 42.99999952 and its whole part is 42. The
        // defence, accumulating a different rounding error, really does reach 46.
        //
        // Neither half works alone. Accumulating in double and truncating loses values *below* the
        // cap that were previously right (47 of 49), and accumulating in Single while rounding
        // changes nothing at all, because the rounding hides the very error that produces the 42.
        // Together they reproduce all 334 published values - 49 below the cap and 285 above, across
        // every level of the five guilds the community tabulated completely. The comment that used
        // to sit here argued the opposite from the double-precision evidence alone.
        return (att, def);
    }

    /// <summary>Highest guild attack and highest guild defense across memberships (they can come from different guilds).</summary>
    public static (int att, int def) BestGuildAD(Character c)
    {
        int a = 0, d = 0;
        foreach (var m in c.Guilds) { var (ga, gd) = GuildAD(m.Guild, m.Level); a = Math.Max(a, ga); d = Math.Max(d, gd); }
        return (a, d);
    }

    // ------------------------------------------------------------------ Hitting

    /// <summary>
    /// fn0DEE's to-hit threshold (MODULE4.BAS:2221-2236): 50/50 at parity, leaning to whichever
    /// side is ahead by its share of the lead, clamped 2..98. A roll of Int(Rnd*100)+1 at or
    /// above it is a miss - so against a defence of 0 the blow lands 97 times in 100.
    /// </summary>
    public static double HitThreshold(int atk, int def)
    {
        double p = atk == 0 && def == 0 ? 0.5
                 : atk > def ? 0.5 + 0.5 * (atk - def) / (double)atk
                 : def == 0 ? 0.5 + 0.5 * atk
                 : 0.5 - 0.5 * (def - atk) / (double)def;
        p *= 100;
        if (p > 98) p = 98;
        if (p < 2) p = 2;
        return p;
    }

    /// <summary>
    /// Falling into a pit, MAZEMOD.BAS:1757: <c>Int(Rnd * (((level + 1) / 2) * 15.5)) *
    /// Log(raceSize + 2) + 1</c>. Deep pits are savage - a tenth level pit reaches into the
    /// nineties for a big race - and the port had been charging one to level-plus-two.
    /// </summary>
    public static int PitDamage(Random rng, int dungeonLevel, int raceSize) =>   // l0270 is an Integer: rounded (MAZEMOD.BAS:1512)
        (int)Math.Round(Math.Floor(rng.NextDouble() * (((dungeonLevel + 1) / 2.0) * 15.5)) * Math.Log(raceSize + 2) + 1, MidpointRounding.ToEven);

    /// <summary>
    /// Landing at the bottom of a chute, MAZEMOD.BAS:127: <c>Int(Rnd * (level * 10.5)) *
    /// Log(raceSize + 2) + 5</c>. The port charged nothing at all for a chute.
    /// </summary>
    /// l00B6 is an Integer, so the product is rounded half to even, not cut.
    public static int ChuteDamage(Random rng, int dungeonLevel, int raceSize) =>
        (int)Math.Round(Math.Floor(rng.NextDouble() * (dungeonLevel * 10.5)) * Math.Log(raceSize + 2) + 5, MidpointRounding.ToEven);

    /// <summary>
    /// Levitation carries you over a pit, but not reliably: MAZEMOD.BAS:1750 only spares you when
    /// <c>5 + (level / 4) &lt; Rnd * 100</c>, so the deeper the pit the likelier you drop into it
    /// anyway. A chute ignores levitation the other way round - MAZEMOD.BAS:1842 simply skips the
    /// damage when you are levitating.
    /// </summary>
    public static bool HoversOverPit(Random rng, int dungeonLevel) =>
        5 + dungeonLevel / 4.0 < rng.NextDouble() * 100;


    /// <summary>
    /// Whether monsters sharing your square decide to attack this second.
    ///
    /// Combat in the original is not turn-based: a timer fires once a second the whole time you
    /// are below ground (timCombat, MAZEMOD.BAS:739), and while monsters are present but not yet
    /// engaged, MULTIMOD.BAS:559-587 rolls this every tick. So you can stand next to monsters, or
    /// walk past them, and they may or may not jump you.
    ///
    /// The base chance comes from the alignment pairing, and your charisma talks them down:
    /// <code>
    ///   l00F0 = 7.5 * Log(CHA)
    ///   Select Case monster alignment
    ///     Case 0 (Good):    char Neutral -> 15,  char Evil -> 100
    ///     Case 1 (Neutral): char Neutral -> 15,  else -> 25
    ///     Case 2 (Evil):    char Evil -> 25,     else -> 35
    ///   If Int(Rnd * 100) + 1 &lt; (l00EE - l00F0) Then they attack
    /// </code>
    /// A good monster facing a good character sets nothing, so the chance stays zero and it never
    /// starts a fight.
    /// </summary>
    public static int EngageChance(Alignment monster, Alignment character, int charisma)
    {
        int b = monster switch
        {
            Alignment.Good => character == Alignment.Neutral ? 15 : character == Alignment.Evil ? 100 : 0,
            Alignment.Neutral => character == Alignment.Neutral ? 15 : 25,
            _ => character == Alignment.Evil ? 25 : 35,
        };
        return (int)(b - 7.5 * Math.Log(Math.Max(1, charisma)));
    }

    /// <summary>One second's roll for whether a waiting group attacks.</summary>
    public static bool MonstersEngage(Random rng, Alignment monster, Alignment character, int charisma) =>
        rng.Next(100) + 1 < EngageChance(monster, character, charisma);

    // ------------------------------------------------------------------ the ailment clock

    /// <summary>
    /// How good you are at working out what you are looking at, <c>mdrcharGetIDSkill</c>
    /// (MAINMOD2.BAS:2502): <c>((INT + WIS) / 2) * Log((topGuildLevel + 5) / 10 + 1)</c>, and zero
    /// if you have no guild level at all.
    /// </summary>
    public static double IdentifySkill(int topGuildLevel, int intelligence, int wisdom)
    {
        if (topGuildLevel <= 0) return 0;
        return (intelligence + wisdom) / 2.0 * Math.Log((topGuildLevel + 5) / 10.0 + 1);
    }

    /// <summary>
    /// The chance, per four-second tick, of pinning down exactly which monster a group holds
    /// (FRMDUNM.BAS:479-487). Darkness costs you a quarter of your skill.
    /// </summary>
    public static int IdentifyChance(double skill, bool dark) =>
        Math.Clamp((int)((dark ? skill * 0.75 : skill) + 20), 10, 90);

    /// <summary>
    /// The coarser pass, on how well you can read the group at all (FRMDUNM.BAS:463-470). Here it
    /// is the *lit* case that gets the bonus, a quarter on top, and the band is 5 to 80.
    /// </summary>
    public static int RecogniseChance(double skill, bool dark) =>
        Math.Clamp((int)((dark ? skill : skill * 1.25) + 20), 5, 80);

    /// <summary>
    /// Standing in water or quicksand costs you hits equal to the dungeon level, every tick of the
    /// ailment clock, unless you are levitating (FRMDUNM.BAS:500-525).
    /// </summary>
    public const int WaterHurts = 1;

    /// <summary>
    /// Quicksand also eats your pack: <c>Rnd(100) &lt; (dungeonLevel / 25) * 100</c>, then a random
    /// slot of the forty, and it goes if it is not a guild quest item.
    /// </summary>
    public static bool QuicksandTakesSomething(Random rng, int dungeonLevel) =>
        rng.Next(100) < dungeonLevel / 25.0 * 100;


    // ------------------------------------------------------------------ Damage

    /// <summary>
    /// <c>fn0DEE</c> (MONSMOD.BAS:2212), the one damage function both sides use. Attack and
    /// defence set the chance to hit (2..98) and a small adjustment; level and strength set the
    /// size of the blow, each tapered by its own log curve; the modifier the caller built scales
    /// it, tapered too once it is above 1. A miss, or a blow that came to nothing, is 0. The four
    /// random draws are made in the original's order, so a seeded run reproduces it exactly.
    /// </summary>
    public static int Fn0DEE(Random rng, int atk, int def, int level, int strength, double damMod)
    {
        double threshold = HitThreshold(atk, def);
        if (damMod <= 0) damMod = 0.1;
        int str = strength < 1 ? 1 : strength;
        // CInt rounds half to even; Int() floors
        str = (int)Math.Round(str - Math.Log(str) * ((Math.Log(str) - 2.4) * 2.25), MidpointRounding.ToEven) + 1;
        if (damMod > 1) damMod -= Math.Log(damMod) * Math.Log(damMod);
        double lvlTerm = (Math.Log(level + 5) + 1) / Math.Log(1.2);
        double strTerm = str / 30.0;
        double roll = Math.Floor(rng.NextDouble() * ((str + 10) / 2.0) + (str + 10) / 4.0) / 10;
        int dmg = (int)Math.Floor(lvlTerm * strTerm * roll * damMod);
        // the attack/defence term: with defence 0 it is Int(-atk/8), pulled back up by a random
        // share of itself and one more, and taken off the damage 999 times in 1000. Taking off a
        // negative number is what the "+ Rnd[1 to Atk/8]" the fan formulas quote really is.
        int adj = (int)Math.Floor(((def - atk) / 2.0) / 4.0);
        adj = adj - (int)Math.Floor(rng.NextDouble() * (adj + 1)) + 1;
        if ((int)(rng.NextDouble() * 1000) + 1 > 1) dmg -= adj;
        if (dmg < 1) dmg = 0;
        if ((int)(rng.NextDouble() * 100) + 1 >= threshold) dmg = 0;
        return dmg;
    }

    /// <summary>
    /// A character's blow: the modifier sub1019 builds (MODULE4.BAS:379-431) - the fighting
    /// ability term, the weapon's damage modifier, +5 for a critical and +2 for a backstab, half
    /// against a target you cannot see, and a tenth of the size difference - then
    /// <see cref="Fn0DEE"/>. The original passes the monster's defence as an unassigned local, so 0.
    /// </summary>
    public static int CharacterDamage(Random rng, int guildLevel, double fightingMod, double weaponMod, int strength, int sizeDiff, bool crit, bool backstab, int atk, bool invisibleTarget = false, int def = 0)
    {
        double damMod = FightingBase + ((Math.Log(100 + guildLevel) / 1.75 - 2.3) * fightingMod * fightingMod / 2);
        damMod *= weaponMod <= 0 ? 1 : weaponMod;
        if (crit) damMod += 5;
        if (backstab) damMod += 2;
        if (invisibleTarget) damMod -= damMod / 2;
        if (sizeDiff < 0) damMod -= -sizeDiff / 10.0;
        else if (sizeDiff > 0) damMod += sizeDiff / 10.0;
        return Fn0DEE(rng, atk, def, guildLevel, strength, damMod);
    }

    /// <summary>
    /// A monster's blow once sub0E85 has built the modifier (MONSMOD.BAS:2874-2932):
    /// <see cref="Fn0DEE"/> with its attack, the target's defence, its guild level and its STR.
    /// </summary>
    public static int MonsterBlow(Random rng, int monAtt, int targetDef, int monGuildLevel, int monStr, double damMod) =>
        Fn0DEE(rng, monAtt, targetDef, monGuildLevel, monStr, damMod);

    /// <summary>
    /// A monster's blow for the callers that do not build the modifier themselves - a companion
    /// swinging, a chest that bites back, the probes: its own damage modifier, +0.75 for a chest,
    /// +2 for a critical, +1 for a backstab, and a tenth of the size difference.
    /// </summary>
    public static int MonsterDamage(Random rng, Monster m, int sizeDiff, bool crit, bool backstab, int charDef, bool openingChest = false)
    {
        double damMod = m.DamageMod <= 0 ? 1 : m.DamageMod;
        if (openingChest) damMod += 0.75;
        if (crit) damMod += 2;
        if (backstab) damMod += 1;
        if (sizeDiff > 0) damMod += sizeDiff / 10.0;
        else if (sizeDiff < 0) damMod -= -sizeDiff / 10.0;
        int str = m.Stats.Length > 0 ? m.Stats[0] : 0;
        return Fn0DEE(rng, m.Att, charDef, m.GuildLevel, str, damMod);
    }

    /// <summary>
    /// A monster's or companion's chance of a critical or a backstab, MONSMOD.BAS:2914-2934 for
    /// one swinging at a character and 1100-1134 for a companion swinging at a monster - the same
    /// expression both times. The attacker's guild level over 650 (critical) or 500 (backstab),
    /// times the log of the floor it is found on, less the defender's own level term; clamped
    /// 2..50 for a critical and 4..75 for a backstab. The roll is Int(Rnd * 100) + 1 <= this, so
    /// the fraction counts.
    /// </summary>
    public static double MonsterCritChance(int attackerGuildLevel, int attackerFloor, int defenderLevel, bool backstab)
    {
        double p = (Math.Log(attackerGuildLevel / (backstab ? 500.0 : 650.0) + 1) * 100) * Math.Log(attackerFloor + 1)
                 - Math.Log(defenderLevel / 999.0 + 1) * 100;
        double floor = backstab ? 4 : 2, cap = backstab ? 75 : 50;
        if (p < floor) p = floor;
        if (p > cap) p = cap;
        return p;
    }

    /// <summary>
    /// Whether paralysis wears off this ailment tick, FRMDUNM.BAS:550-560. The paralysis carries
    /// the monster's guild level; against it stand your own level, your CON and your resistance.
    /// It always passes once nothing is left on the square.
    /// </summary>
    public static bool ParalysisPasses(Random rng, int paralysisLevel, int guildLevel, int con, int resistPct, bool monstersPresent)
    {
        if (!monstersPresent) return true;
        double p = Math.Max(1, paralysisLevel);
        double hold = Math.Log(p / 2 + p / 10) / Math.Log(1.02);
        double lvl = Math.Max(1, guildLevel);
        double fight = ((Math.Log(lvl) + lvl / 10) / Math.Log(1.05) / 10 + con * 1.5) + resistPct;
        double chance = hold - fight;
        if (chance < 5) chance = 5;
        if (chance > 90) chance = 90;
        return (int)(rng.NextDouble() * 100) + 1 > chance;
    }


    /// <summary>
    /// The chance of a critical hit, MODULE4.BAS:381-399. The shape is the same for backstabbing
    /// below, and the differences between them are worth naming because they are easy to blur:
    /// critical divides the ability term by <b>three</b>, its weapon term uses <c>Log(10)</c>, its
    /// floor is <b>1</b> and its cap <b>50</b>.
    ///
    /// The weapon term is only added when the weapon actually carries the ability - item effect
    /// bit 4 for critical, bit 5 for backstab - and then the result is at least that term on its
    /// own, so a weapon that crits never does worse than the weapon alone would.
    ///
    /// The two are rolled independently and both can land on the same blow, each adding to the
    /// damage modifier (+5 and +2).
    /// </summary>
    public static double CritChance(int guildLevel, double critMod, int monLevel, int weaponFloor, bool weaponCrits = false) =>
        SpecialHitChance(guildLevel, critMod, monLevel, weaponFloor, weaponCrits,
                         abilityDivisor: 3, weaponLog: 10, floor: 1, cap: 50);

    /// <summary>
    /// Backstabbing, MODULE4.BAS:400-420: the ability term is divided by <b>two</b>, the weapon
    /// term uses <c>Log(100)</c>, the floor is <b>2</b> and the cap <b>75</b>.
    /// </summary>
    public static double BackstabChance(int guildLevel, double bsMod, int monLevel, int weaponFloor, bool weaponBackstabs = false) =>
        SpecialHitChance(guildLevel, bsMod, monLevel, weaponFloor, weaponBackstabs,
                         abilityDivisor: 2, weaponLog: 100, floor: 2, cap: 75);

    private static double SpecialHitChance(int guildLevel, double mod, int monLevel, int weaponFloor,
                                        bool weaponHasIt, double abilityDivisor, double weaponLog,
                                        double floor, double cap)
    {
        double p = ((Math.Log(guildLevel / 999.0 + 1) * 100) * Math.Log(mod + 1)) / abilityDivisor;
        p -= Math.Log(monLevel / 999.0 + 1) * 100;
        p /= 2;

        if (weaponHasIt)
        {
            double fromWeapon = Math.Log(weaponFloor + 1) * Math.Log(weaponLog);
            p = Math.Max(p + fromWeapon, fromWeapon);
        }
        // the floor only applies to someone who can do it at all - by guild level or by weapon
        if (p < floor && (guildLevel > 0 || weaponHasIt)) p = floor;
        if (p > cap) p = cap;
        // MODULE4.BAS:394 compares Int(Rnd * 100) against this Single without rounding it, so a
        // chance of 5.4 really does win six rolls in a hundred. Rounding it first lost that.
        return p;
    }

    // ------------------------------------------------------------------ Experience and gold

    /// <summary>
    /// Experience for disarming a chest's trap, <c>monOpenChest</c> (MONSMOD.BAS:3508-3509):
    ///
    ///     l0502 = 1 + (Chars(0).GuildsJoined(Chars(0).CurrentGuild).Level / 999) * 3
    ///     sub0F11 pv04F2, CLng((abilityChance(pv04F2, m04E0, False) * (Monsters(Fight.M4F24).GuildLevel / 2)) / l0502)
    ///
    /// `(level / 999) * 3` is `level / 333`, so the divisor is the reciprocal of the
    /// Experience_Awarded_Formula page's GuildLevelFactor and this is the page's
    /// `CInt(ChanceOfDisarming * MonsterGuildLevel * 0.5 * GuildLevelFactor)` exactly - with the
    /// page's own note that the chance is 5..98 rather than 0.05..0.98.
    ///
    /// Two things the page does not say and the source does. Nothing is earned for a chest that was
    /// not trapped, because the award sits inside the `Fight.ChestTrap &gt; 0` branch. And the guild
    /// level is <c>Chars(0)</c>'s - the party's first character - while the chance and the award
    /// itself are the opener's. Whoever opens the chest, the discount for being high level is taken
    /// from whoever is first in the party. That is a fault and it is reproduced; see
    /// docs/FIDELITY.md.
    /// </summary>
    public static long ChestExp(double disarmChance, int monsterGuildLevel, int firstCharacterGuildLevel, int partySize = 1)
    {
        double xp = disarmChance * (monsterGuildLevel / 2.0) / (1 + firstCharacterGuildLevel / 333.0);
        double party = 1 - (partySize - 1) * 0.04;
        return (long)Math.Round(party * Math.Round(xp, MidpointRounding.ToEven), MidpointRounding.ToEven);
    }

    /// <summary>
    /// <c>sub0CDF</c> (MONSMOD.BAS:174-181), which the Experience_Awarded_Formula page states in a
    /// tidied-up form:
    ///
    ///     l00B2 = 1 + (Chars(c).GuildsJoined(Chars(c).CurrentGuild).Level / 999) * 3
    ///     l00B0 = ((((hits) * (.9# - (Log(hits + 1) / 10))) * (5 + (Monsters(m).GuildLevel / 10))
    ///              * (2 - ((Log(hits) / 10)))) * companionFactor) / l00B2
    ///
    /// Take the tenths out of all three brackets and that is the page's `hits * (9 - LN(hits+1)) *
    /// (20 - LN(hits)) * (MonsterGuildLevel + 50) * CompanionFactor * GuildLevelFactor / 1000`.
    /// The party-size factor is applied afterwards by <c>sub0F11</c> (MAINMOD2.BAS:289) as
    /// `p0098 * (1 - ((UBound(Chars) * 2) * .02#))`, and UBound is one less than the party size, so
    /// it is the page's `1 - ((PartySize - 1) * 0.04)`. Both roundings are VB's, half to even.
    /// </summary>
    public static long ExpAwarded(int hitsInflicted, int monGuildLevel, int currentGuildLevel, bool byCompanion, int partySize = 1)
    {
        if (hitsInflicted <= 0) return 0;
        double h = hitsInflicted;
        double xp = h * (9 - Math.Log(h + 1)) * (20 - Math.Log(h)) * (monGuildLevel + 50) * (byCompanion ? 0.25 : 1.0) * (1.0 / (1 + currentGuildLevel / 333.0)) / 1000;
        double party = 1 - (partySize - 1) * 0.04;
        return (long)Math.Round(party * Math.Round(xp, MidpointRounding.ToEven), MidpointRounding.ToEven);
    }

    /// <summary>
    /// <c>fn12A1</c> (MAINMOD2.BAS:2118-2138), line for line: the experience the character at
    /// guild level <paramref name="level"/> needs for the next one. Four of its locals are Currency
    /// - VB's fixed point, four decimal places, a Double landing in one rounded half to even - and
    /// the Single <c>l038A</c>/<c>l038C</c> are the guild's and the race's Exp fields, the race's
    /// plus one. Only the last pass of the loop counts (l0394 is never read), and the Long it hands
    /// back is the Currency rounded half to even. The Experience_Required_for_Level_Formula page's
    /// closed form of the same thing was off by one in 12 of the 16,800 guild, race and level cases
    /// to 200; its anchors (Human Nomad 872,326 and 556,907) are held in selfcheck. The x87's 80-bit intermediates make no difference to any shipped case once the last line's
    /// Single quotient is left unnarrowed, as the x87 leaves it (Mordor.Tools x87).
    /// </summary>
    public static long Fn12A1(int level, float guildExp, float raceExp)
    {
        static long Cur(double x) => (long)Math.Round(x * 10000.0, MidpointRounding.ToEven);   // ten-thousandths
        long l0392 = 0;
        float l038A = guildExp;
        float l038C = raceExp + 1;
        for (int l0386 = 1; l0386 <= level; l0386++)
        {
            long l038E = Cur((float)(l0386 + l0386) * l038A);            // Integer * Single is a Single
            long l0390 = Cur((float)l0386 / 2 * 2 + (l0386 - 1));         // l0386 / 2: Integer / Integer is a Single
            // (l038E@ / 12) * 500 * l0386 is a Double; 344 * l0390@ stays Currency until the sum
            l0392 = Cur((l038E / 10000.0 / 12 * 500 * l0386 + 344 * l0390 / 10000.0 - 2) / 10);
        }
        // ((Log(l038C) / Log(l038C + 1)) + 1) - .7# + (l038C / 25). VB types the last a Single, but
        // the line is one typed expression, which the x87 evaluates without narrowing a step to its
        // type - the reading fn1485's measured 208 settles (Fn1485Price). Narrowed, 0.16 becomes
        // 0.1599999964 and about a third of the shipped guild, race and level cases (1-1000) come out
        // one off; unnarrowed, Doubles agree with a 64-bit-mantissa evaluation in every one of them
        // (Mordor.Tools x87).
        double f = ((Math.Log(l038C) / Math.Log((float)(l038C + 1))) + 1) - .7 + l038C / 25.0;
        l0392 = Cur(l0392 / 10000.0 * f);
        return (long)Math.Round(l0392 / 10000m, 0, MidpointRounding.ToEven);    // fn12A1 = l0392@
    }

    /// <summary>
    /// <c>fn13ED</c> (MAINMOD2.BAS:2710-2714): the level raised by one, fn12A1 asked, one taken off -
    /// the most experience the character can hold at their level.
    /// </summary>
    public static long Fn13ED(int level, float guildExp, float raceExp) => Fn12A1(level + 1, guildExp, raceExp) - 1;

    /// <summary>
    /// <c>fn12B2</c> (MAINMOD2.BAS:2140-2155), line for line: the gold the guildmaster wants from a
    /// character at guild <paramref name="level"/> to make the next one. <paramref name="guildGold"/>
    /// is the guild record's Integer <c>Gold</c> field (MAIN.TXT:211), loaded as GuildDef.GoldFactor.
    /// The race's Exp is read into l03A2 and never used, so it is not asked for here. Types as VB has
    /// them: Integer / Integer is a Single, a Single or a Long meeting a Double is a Double, and
    /// every store into the Long locals (l03A6, l03A8, the result) rounds half to even. Both Long
    /// lines are typed expressions (the Variant counter l039C is not in them), so they are evaluated
    /// as the x87 evaluates them - 64-bit steps, nothing narrowed to its VB type, rounded only at the
    /// store (<see cref="X87"/>, the reading fn1485's measured 208 settles). It matters: 60.2# is a
    /// shade over 60.2, so every term <c>(2i - 1) * 30.1</c> that is a whole number and a half - 150.5
    /// at i = 3 - is a shade over the half on the x87 and rounds up, where a Double lands on the half
    /// and rounds to even; and <c>Gold / 10</c> stays unnarrowed. Nomad from level 3 pays 352, not 351.
    /// </summary>
    public static long Fn12B2(int level, int guildGold)
    {
        short l039E = (short)level;                                                  // Integer
        var L = X87.Of((long)l039E);
        var halfL = L / X87.Of(2L);                                                  // l039E / 2, unnarrowed
        var rate = X87.Of(60.2);                                                     // 60.2#
        long l03A8 = 0;
        for (int l039C = 1; l039C <= l039E; l039C++)
        {
            // (l039C / 2) * 2 + (l039C - 1) through the Variant counter: whole or a half, exact either way
            long l03A6 = (long)Math.Round((double)l039C / 2 * 2 + (l039C - 1), MidpointRounding.ToEven);
            // l03A8& = l03A8& + (((l039E / 2) * l03A6&) * 60.2#) / l039E
            l03A8 = (X87.Of(l03A8) + halfL * X87.Of(l03A6) * rate / L).RoundToLong();
        }
        // fn12B2 = l03A8& + (l03A8& * (Gold / 10))
        var acc = X87.Of(l03A8);
        return (acc + acc * (X87.Of((long)(short)guildGold) / X87.Of(10L))).RoundToLong();
    }

    /// <summary>
    /// Cost to join a guild after the first free one, FRMCTYG.BAS:115-127: (Σ(ability+1)×1.25 +
    /// Σ spellMax/3)² × 5356. The Guild_Join_Cost_Formula page agrees to the gold piece.
    /// </summary>
    public static long GuildJoinCost(GuildDef g)
    {
        double sum = 0;
        // abilities 0 to 5 (mc006A = 6) and spell classes 0 to 17 (mc0074 = 18), FRMCTYG.BAS:116-125
        foreach (var a in g.Abilities.Take(6)) if (a > 0) sum += Math.Round((a + 1) * 1.25, 4);
        foreach (var m in g.SpellMax.Take(18)) if (m > 0) sum += Math.Round(m / 3.0, 4);
        return (long)Math.Round(sum * sum * 5356);
    }

    // ------------------------------------------------------------------ Spells

    /// <summary>Spell level for a guild level: Int(level / 2 + 0.5), 1..255.</summary>
    public static int SpellLevel(int guildLevel) => Math.Clamp((int)(guildLevel / 2.0 + 0.5), 1, 255);

    /// <summary>
    /// One guild's price in <c>fn1485</c> (MAINMOD2.BAS:2868-2893):
    /// <code>
    ///     l0488 = M55F6(Kind, 0)  : If l0488 = 0 Then l0488 = 1     ' a Single (2872)
    ///     l0484 = l0484 * l0488                                      ' into an Integer (2870)
    ///     Int(((l0484 / l048A) * SpellDefs(s).M556D) * l0488 * 1.9#)
    /// </code>
    /// The rate is the guild record's Single (MAIN.TXT:209), taken as stored. The store into the
    /// Integer <c>l0484</c> rounds half to even. The expression is floored by <c>Int</c> as the x87
    /// holds it, with no rounding to a Single or a Double in between: the community measured an
    /// exact 209 coming out 208 (Spell_Cost_Formula, "209 is stored as 208.9999..."). That is the
    /// signature of <c>1.9#</c>, the Double nearest 1.9 (1.8999999999999999112), multiplied with
    /// no rounding to a Double after it: 110 times it rounds back to exactly 209 as a Double, and
    /// rounding each step to its VB type (Singles, then the Double product) gives 209 as well. An
    /// evaluator that keeps the Double product unrounded keeps the Single steps unrounded too, so
    /// this floors the exact product, <c>rated * M556D * rate * 19 / (10 * SL)</c>, less one when
    /// that is a whole number: 1.9# is a shade under 1.9, and the x87's 64-bit mantissa keeps
    /// the shade (relatively 5E-17, against roundings of about 2E-19). When it is not whole it is
    /// at least 1 / (10 * SL * 2^k) from one, k the rate's fraction bits, far more than either.
    /// Confirmed in the running original (2026-10-25): the Points column of Objects &amp; Spells matched
    /// this in every case read, nine of them where rounding each step would give one more - Shock 18
    /// for a new Sorcerer, Charm of Opening 143 at SL 19 (docs/FIDELITY.md; selfcheck "measured").
    /// The page's "- 0.0001" agrees except where the true cost is a hair (up to 0.0001) over a
    /// whole number, which the page floors one lower.
    /// <paramref name="rated"/> is <c>l0484</c> after the multiply, which fn1485 keeps as the floor.
    /// </summary>
    public static int Fn1485Price(int level, int u4, float rate, int sl, out int rated)
    {
        float r = rate == 0 ? 1f : rate;                                         // 2886-2889
        rated = (int)Math.Round((double)level * r, MidpointRounding.ToEven);     // 2890: exact, then the Integer store
        // the Single rate as mant * 2^exp, exactly
        int bits = BitConverter.SingleToInt32Bits(r);
        int e = (bits >> 23) & 0xFF;
        long mant = bits & 0x7FFFFF;
        if (e != 0) mant |= 0x800000; else e = 1;
        if (bits < 0) mant = -mant;
        int exp = e - 127 - 23;
        var num = (System.Numerics.BigInteger)rated * u4 * mant * 19;
        var den = (System.Numerics.BigInteger)10 * Math.Max(1, sl);
        if (exp >= 0) num <<= exp; else den <<= -exp;
        var q = System.Numerics.BigInteger.DivRem(num, den, out var rem);
        if (rem.Sign < 0) q -= 1;                      // a floor, not a truncation
        else if (rem.IsZero && num.Sign > 0) q -= 1;   // whole: the true product is just under it
        return (int)q;
    }

    /// <summary>
    /// A damage spell, <c>monGetSpellHits</c> (MONSMOD.BAS:2464-2533): base = X in "Firebolt(10/1)"
    /// (M5B33), power = Y (M5B41 + 1). The Spell_Damage_Formula page agrees.
    /// </summary>
    public static int SpellDamage(Random rng, int baseDmg, int power, int sl, int resistPct)
    {
        sl = Math.Max(1, sl);
        double mod1 = baseDmg + (baseDmg * Math.Log(sl + 1) / Math.Log(5)) + (int)((sl / 10.0) * (power - 1));
        int mod2 = (int)(rng.NextDouble() * (mod1 - ((sl - 1) / (double)(sl * sl))) / 2) + 1;
        int dmg = (int)(mod1 - mod2);
        // `monGetSpellHits` (MONSMOD.BAS:2464-2533) holds the running damage in `l0432 As Integer`
        // and applies resistance as `l0432 = l0432 * (1 - (Resistances(i) / 100))`, so that step
        // rounds on the way back into the Integer rather than truncating. The community page writes
        // it as plain multiplication because the rounding is in the declaration, not the line.
        //
        // One routine serves both directions - MAINMOD2.BAS:1467 is the character casting and
        // MONSMOD.BAS:1046 a monster - so this is the same rule the port already applies on the
        // monster side in MonsterAttack.MonsterSpellHits.
        return Math.Max(0, (int)Math.Round(dmg * (1 - Math.Clamp(resistPct, 0, 100) / 100.0),
                                           MidpointRounding.ToEven));
    }

    /// <summary>A healing spell, mdrcharCastSpell's heal branch (MAINMOD2.BAS:952-961): Log(SL) / Log(10) where damage has Log(SL + 1) / Log(5).</summary>
    public static int SpellHeal(Random rng, int baseHeal, int power, int sl)
    {
        sl = Math.Max(1, sl);
        double mod1 = baseHeal + (baseHeal * Math.Log(sl) / Math.Log(10)) + (int)((sl / 10.0) * (power - 1));
        int mod2 = (int)(rng.NextDouble() * (mod1 - ((sl - 1) / (double)(sl * sl))) / 2) + 1;
        return Math.Max(1, (int)(mod1 - mod2));
    }

    /// <summary>
    /// The expected value of <see cref="SpellHeal"/>, worked out exactly from the same formula
    /// rather than sampled. The only random term is <c>mod2 = Int(U × R / 2) + 1</c> with U uniform
    /// on [0, 1) and R = mod1 − (SL − 1) / SL²: with h = R / 2 the whole number Int(U × h) is k
    /// with probability (min(k + 1, h) − k) / h for k = 0 .. ⌈h⌉ − 1, and each k heals
    /// Max(1, Int(mod1 − k − 1)). Nothing is capped at the hits the target is missing. A port
    /// helper (the Kaizen mod's heal choice), not a rule of the game's: nothing in the game
    /// averages a heal.
    /// </summary>
    public static double SpellHealAverage(int baseHeal, int power, int sl)
    {
        sl = Math.Max(1, sl);
        double mod1 = baseHeal + (baseHeal * Math.Log(sl) / Math.Log(10)) + (int)((sl / 10.0) * (power - 1));
        double h = (mod1 - ((sl - 1) / (double)(sl * sl))) / 2;
        // U × h is then nought or below: the Int is nought (a heal always has a base above nought,
        // so this is only a guard)
        if (!(h > 0)) return Math.Max(1, (int)(mod1 - 1));
        double sum = 0;
        for (int k = 0; k < h; k++)
            sum += (Math.Min(k + 1, h) - k) / h * Math.Max(1, (int)(mod1 - (k + 1)));
        return sum;
    }

    // ------------------------------------------------------------------ Misc

    /// <summary>The guild abilities, in the order MDATA1 stores them and the rating site lists them.</summary>
    public static readonly (string Name, int Index)[] Abilities =
    {
        ("Thieving", 0), ("Backstabbing", 1), ("Critical Hit", 2),
        ("Multiple Swings", 3), ("Fighting", 4), ("Perception", 5),
    };

    /// <summary>mc0080 (MODULE6.BAS:8), the constant term in the fighting ability.</summary>
    public const double FightingBase = 0.6;

    // ------------------------------------------------------------------ what a floor can throw at you

    /// <summary>
    /// How far back below the target floor an encounter may reach, MODULE4.BAS:1481. The original
    /// means to roll one, two or three:
    /// <code>
    ///   m026A = Int(Rnd * 100) + 1
    ///   If m026A &lt; 50 Then m027C = 1 ElseIf m026A &lt; 30 Then m027C = 2 ElseIf m026A &lt; 10 Then m027C = 3
    /// </code>
    /// but the ladder is the wrong way round - anything under 30 is already under 50 - so the last
    /// two branches can never run. The spread is one 49 percent of the time and *nought* the rest,
    /// before the depth term. Reproduced, not corrected.
    ///
    /// Then <c>m027C = m027C + level / 5</c>, which is float division rounded into an Integer, and
    /// finally capped at the level itself.
    /// </summary>
    public static int EncounterSpread(int level, Random rng, bool onStud)
    {
        int roll = rng.Next(100) + 1;
        int spread = (int)Math.Round((roll < 50 ? 1 : 0) + level / 5.0, MidpointRounding.ToEven);
        // and then, on a stud square, the whole thing is usually replaced by one (MODULE4.BAS:1491)
        if (onStud && rng.NextDouble() * 75 < 50) spread = 1;
        return Math.Min(spread, level);
    }

    /// <summary>
    /// The floor an encounter is drawn from. A "stud" square - bit 22, and the community's
    /// MDATA11 notes give it that name and that bit - draws from one level deeper, which is the
    /// only way the ten monsters filed at floor 16 can ever be met (MODULE4.BAS:1432).
    /// </summary>
    public static int EncounterFloor(int level, bool onStud) => onStud ? level + 1 : level;

    // ---------------------------------------------------------- ability ratings, fn123F/fn13AA/fn135B

    /// <summary>
    /// The 1-to-100 rating the Guild tab shows, <c>fn123F</c> (MODULE6.BAS:1982). It is the raw
    /// ability placed between the weakest and strongest a character could possibly be at it -
    /// <c>((value - floor) / (ceiling - floor)) * 100</c>, never below 1.
    ///
    /// The floor and ceiling come from <c>fn13AA</c> and <c>fn135B</c>, which are the same four
    /// formulas evaluated at fixed extremes rather than at your numbers.
    /// </summary>
    public static int AbilityRatingExact(Character c, int ability, Func<short, Item?>? gear = null)
    {
        double value = BestAbilityValue(c, ability, gear);
        double lo = AbilityFloor(ability), hi = AbilityCeiling(ability);
        if (hi <= lo) return 1;
        int r = (int)Math.Round((value - lo) / (hi - lo) * 100, MidpointRounding.ToEven);
        return r <= 1 ? 1 : r;
    }

    /// <summary>
    /// gv0652, MODULE1.BAS:499-505: what the Guild tab calls the six abilities, in the order the
    /// guild table holds their modifiers.
    /// </summary>
    public static readonly string[] AbilityNames =
        { "Thieving", "Backstabbing", "Critical Hit", "Multiple Swings", "Fighting", "Perception" };

    /// <summary>
    /// "which guild the ability comes from" (help, Guild tab). <c>fn122A</c> tries every guild the
    /// character belongs to and keeps the best chance, ties going to the earlier guild in the
    /// table; this is that same loop, reporting the winner rather than the number.
    /// </summary>
    public static string BestAbilityGuild(Character c, int ability, Func<short, Item?>? gear = null)
    {
        var (wis, dex) = AbilityStats(c, ability, gear);
        double bestChance = double.NegativeInfinity;
        string best = "";
        foreach (var g in GuildData.All)
        {
            var m = c.Guilds.FirstOrDefault(x => x.Guild == g.Name);
            if (m == null || m.Level <= 0) continue;
            double gm = g.Abilities.Length > ability ? g.Abilities[ability] : 0;
            double chance = AbilityChance(ability, gm, m.Level, wis, dex, 0, 2);
            if (chance > bestChance) { bestChance = chance; best = g.Name; }
        }
        return best;
    }

    /// <summary>
    /// The raw ability number <c>fn123F</c> rates, taken from whichever guild gives the best
    /// <see cref="AbilityChance"/>. Note this is *not* that chance: the thieving and perception
    /// cases drop the dungeon-level opposition and the clamps, because a rating describes the
    /// character, not the square they happen to be standing on.
    /// </summary>
    public static double BestAbilityValue(Character c, int ability, Func<short, Item?>? gear = null)
    {
        // fn122A is called with its third argument True, which is what turns the opposition off;
        // it leaves the winning guild's modifier and level in gv141C, and fn123F reads those back
        var (wis, dex) = AbilityStats(c, ability, gear);
        double bestChance = double.NegativeInfinity, mod = 0; int level = 1;
        foreach (var g in GuildData.All)                 // table order: fn122A's ties go to the lower index
        {
            var m = c.Guilds.FirstOrDefault(x => x.Guild == g.Name);
            if (m == null || m.Level <= 0) continue;
            double gm = g.Abilities.Length > ability ? g.Abilities[ability] : 0;
            double chance = AbilityChance(ability, gm, m.Level, wis, dex, 0, 2);
            if (chance > bestChance) { bestChance = chance; mod = gm; level = m.Level; }
        }
        if (double.IsNegativeInfinity(bestChance)) return 0;

        double lvl = Math.Max(1, level);
        switch (ability)
        {
            case 0:
            {
                double stat = wis / 4.0 + dex / 2.0;
                double against = (Math.Log(2) * 4) * (Math.Log(2) * 1.25) * 1.25;
                return (stat + (4.5 + Math.Exp(Math.Log(lvl + 1) / Math.Log(15))) * (mod / 10 * 14)) - against * 1.5;
            }
            case 4:
                return FightingBase + (((Math.Log(100 + lvl) / 1.75) - 2.3) * (mod * mod)) / 2;
            case 5:
                return (mod + Math.Log(lvl + 1) / Math.Log(1.25)) * (mod * 0.3) + wis;
            default:
                return ((Math.Log(lvl / 999.0 + 1) * 100) * Math.Log(mod + 1)) / 3;
        }
    }

    /// <summary>The weakest anyone could be at an ability, <c>fn13AA</c> (MODULE6.BAS:2635).</summary>
    public static double AbilityFloor(int ability)
    {
        double mod = LowestGuildModifier(ability);
        switch (ability)
        {
            case 0:
            {
                // note the hardcoded 5 here: the floor does not use the lowest guild's modifier
                double against = (Math.Log(2) * 4) * (Math.Log(2) * 1.25) * 1.25;
                return (2 / 4.0 + 2 / 2.0 + (4.5 + Math.Exp(Math.Log(2) / Math.Log(15))) * (5 / 10.0 * 14)) - against * 1.5;
            }
            case 4:
                return FightingBase + (((Math.Log(101) / 1.75) - 2.3) * (mod * mod)) / 2;
            case 5:
                return (mod + Math.Log(2) / Math.Log(1.25)) * (mod * 0.3) + 8 / 4.0;
            default:
                return ((Math.Log(1 / 999.0 + 1) * 100) * Math.Log(mod + 1)) / 3;
        }
    }

    /// <summary>The strongest anyone could be at an ability, <c>fn135B</c> (MODULE6.BAS:2590).</summary>
    public static double AbilityCeiling(int ability)
    {
        const int Top = 999;
        double mod = HighestGuildModifier(ability);
        switch (ability)
        {
            case 0:
            {
                // and a hardcoded 9 here, with stats of 30 and 40 rather than any real maximum
                double against = (Math.Log(2) * 4) * (Math.Log(2) * 1.25) * 1.25;
                return (30 / 4.0 + 40 / 2.0 + (4.5 + Math.Exp(Math.Log(Top + 1) / Math.Log(15))) * (9 / 10.0 * 14)) - against * 1.5;
            }
            case 4:
                return FightingBase + (((Math.Log(100 + Top) / 1.75) - 2.3) * (mod * mod)) / 2;
            case 5:
                return (mod + Math.Log(Top) / Math.Log(1.25)) * (mod * 0.3) + 60 / 4.0;
            default:
                return ((Math.Log(Top / 999.0 + 1) * 100) * Math.Log(mod + 1)) / 3;
        }
    }

    /// <summary>
    /// <c>fn13AA</c>'s opening loop, which is odder than it looks: it takes the minimum against a
    /// starting zero, so unless some guild's modifier is negative the minimum stays zero - and
    /// then the next line replaces it with guild 0's. The Nomad is the floor in practice.
    /// </summary>
    private static double LowestGuildModifier(int ability)
    {
        double lowest = 0;
        foreach (var g in GuildData.All)
            if (g.Abilities.Length > ability && g.Abilities[ability] < lowest) lowest = g.Abilities[ability];
        double first = GuildData.All.Count > 0 && GuildData.All[0].Abilities.Length > ability ? GuildData.All[0].Abilities[ability] : 0;
        return first > lowest ? first : lowest;
    }

    private static double HighestGuildModifier(int ability)
    {
        double highest = 0;
        foreach (var g in GuildData.All)
            if (g.Abilities.Length > ability && g.Abilities[ability] > highest) highest = g.Abilities[ability];
        return highest;
    }

    // The community's published rating formula used to live here. It matched the one screenshot
    // data point but was, by its own admission, an approximation for the other five abilities;
    // AbilityRatingExact above is fn123F itself, and reproduces both that screenshot and the
    // help's worked example that only the Warrior reaches 100 fighting.

    /// <summary>One click of the store's ID button moves an item up one level.</summary>
    public static IdLevel NextIdLevel(IdLevel now) => now switch
    {
        IdLevel.Barely => IdLevel.Partial,
        IdLevel.Partial => IdLevel.Mostly,
        _ => IdLevel.All,
    };

    /// <summary>Chance (percent) of losing a CON point on a morgue raise: <c>Rnd * 100 &lt; (50 - CON * 2)</c>, natural CON (MAINMOD2.BAS:358, 3113).</summary>
    public static int RaiseConLossChance(int con) => Math.Max(0, 50 - con * 2);

    // ------------------------------------------------------------------ monster gold
    // The original decides an encounter's treasure when it *generates* the monsters, not per kill,
    // and hands it out afterwards in monGetGold. All of this is MONSMOD.BAS in the decompiled
    // source; the field names below are the ones pinned in tools/NameMap/names.tsv.

    /// <summary>
    /// Which class of treasure chest this encounter rolled, from the monster's BoxChance
    /// percentages. The generator rolls once and walks the two middle entries, the rarer one
    /// winning, so the result is 0, 2 or 3 - class 1 is never produced, though the gold
    /// multiplier still has a case for it:
    /// <code>
    ///   m026A = Rnd * 99
    ///   M4F1B = 0
    ///   If this is the level's lair monster And BoxChance(2) > 0 Then m029A = m029A + 25
    ///   For m0258 = 1 To 2
    ///       If m026A &lt; BoxChance(m0258) + m029A Then M4F1B = m0258 + 1
    ///   Next
    /// </code>
    /// </summary>
    public static int ChestClass(Monster m, double roll0To99, bool isLairMonster)
    {
        int bonus = isLairMonster && m.BoxChance.Length > 2 && m.BoxChance[2] > 0 ? 25 : 0;
        int cls = 0;
        for (int i = 1; i <= 2 && i < m.BoxChance.Length; i++)
            if (roll0To99 < m.BoxChance[i] + bonus) cls = i + 1;
        return cls;
    }

    /// <summary>
    /// What one group of identical monsters contributes:
    /// <code>
    ///   m02A4 = ((GoldFactor * (10 ^ (GoldFactor - .5))) / 4)
    ///   m02A4 = ((m02A4 / 4) + (Rnd * (m02A4 / 2)) + (Rnd * (m02A4 / 2))) * (Log((Floor + 1) / Log(2)))
    ///   If m02A4 &lt; 500 Then m02A4 = Int(Rnd * 500) + 2
    ///   total = total + m02A4 * (Log(count + 1) * (Log(count + 1) / 2))
    /// </code>
    /// The depth term is written <c>Log((Floor + 1) / Log(2))</c> - the division is inside the
    /// logarithm. That is almost certainly a slip for log base 2, but it is what the game computes,
    /// so it is what this computes.
    /// </summary>
    public static double GroupGold(Monster m, int count, Random rng)
    {
        double g = Math.Max(0, m.GoldFactor);
        double a = (g * Math.Pow(10, g - 0.5)) / 4;
        a = (a / 4 + rng.NextDouble() * (a / 2) + rng.NextDouble() * (a / 2))
            * Math.Log((m.LevelFound + 1) / Math.Log(2));
        if (a < 500) a = (int)(rng.NextDouble() * 500) + 2;
        double n = Math.Log(count + 1);
        return a * (n * (n / 2));
    }

    /// <summary>
    /// The whole encounter's gold. After every group has been added the generator scales by half
    /// the number of monsters present and, if the chest is locked, by another half again; monGetGold
    /// then applies the chest class. A chest can only be locked if the class is above 1, and the
    /// chance is the monster's LockedChance.
    /// </summary>
    public static long EncounterGold(IReadOnlyList<Monster> kinds, IReadOnlyList<int> counts, Random rng, int chestClass, bool chestLocked)
    {
        if (kinds.Count == 0) return 0;
        double gold = 0;
        for (int i = 0; i < kinds.Count; i++) gold += GroupGold(kinds[i], counts[i], rng);

        int total = 0;
        for (int i = 0; i < counts.Count; i++) total += counts[i];
        gold *= total / 2.0;

        // the class and the lock are decided once, by the caller, on the single roll the original
        // makes at MONSMOD.BAS:1692-1697; rolling them again here paid a locked chest twice
        if (chestLocked) gold *= 1.5;
        gold *= chestClass switch { 0 or 1 => 0.5, 2 => 1.0, 3 => 1.5, _ => 1.0 };
        return (long)Math.Max(0, gold);
    }

    /// <summary>
    /// Death by old age, checked every turn you spend in the dungeon (FRMDUNM.BAS):
    /// <code>
    ///   If (((Age / 365) / race.Age) * 100) >= 98 Then
    ///       If Int(Rnd * 100) &lt; 5 + Log(((((Age / 365) / race.Age) * 100) - 98)) Then
    ///           mdrcharSetDead ... "You have died of natural causes."
    /// </code>
    /// The race record's own Age field is its lifespan in years. Once past 98% of it the chance is
    /// about 5% a turn, rising only slowly - Log(1) at 99%, Log(12) at 110%. The one guard added
    /// here is against Log(0) exactly at 98%, which would be a runtime error in the original.
    /// </summary>
    public static bool DiesOfOldAge(Random rng, int ageDays, int raceLifespanYears)
    {
        if (raceLifespanYears <= 0) return false;
        double pct = (ageDays / 365.0) / raceLifespanYears * 100.0;
        if (pct < 98) return false;
        return rng.Next(100) < 5 + Math.Log(Math.Max(pct - 98, 1e-9));
    }

    // ------------------------------------------------------------------ guild abilities, fn122A

    /// <summary>
    /// The raw percentage chance a guild ability gives, <c>fn122A</c> in MAINMOD2.BAS. The caller
    /// takes the best across every guild the character belongs to. Ability 0 is thieving (which is
    /// what disarms a chest) and 5 is perception; both are opposed by where you are and what you
    /// are facing, and both are clamped. The others are flat products of the guild's own rating and
    /// the level in it.
    /// </summary>
    /// <summary>
    /// The WIS and DEX terms an ability formula wants, with the gear folded in the way fn122A
    /// folds it: thieving reads <c>(M55B5 + M584B)</c> for both, i.e. the effective stat, while
    /// perception reads <c>M55B5(2) + M584B(2) / 4</c> - the natural WIS plus a quarter of what
    /// the gear adds. With no gear lookup the natural stats are used, which is exact in town
    /// only for a character wearing nothing that changes them.
    /// </summary>
    public static (double wis, double dex) AbilityStats(Character c, int ability, Func<short, Item?>? gear)
    {
        int natWis = c.Stats[(int)Stat.Wis], natDex = c.Stats[(int)Stat.Dex];
        if (gear == null) return (natWis, natDex);
        // Stats + StatMods with no floor: StatMods is never clamped (MAINMOD2.BAS:1769-1774)
        int effWis = c.StatWithMods((int)Stat.Wis, gear), effDex = c.StatWithMods((int)Stat.Dex, gear);
        return ability == 5 ? (natWis + (effWis - natWis) / 4.0, effDex) : (effWis, effDex);
    }

    public static double AbilityChance(int ability, double guildRating, int guildLevel,
                                       double wis, double dex, int dungeonLevel, int monsterGuildLevel)
    {
        double lvl = Math.Max(1, guildLevel);
        switch (ability)
        {
            case 0:     // thieving - disarming, and picking what is locked
            {
                double stat = wis / 4.0 + dex / 2.0;
                // in town there is nothing to oppose you: dungeonLevel is 0, so Log(0 + 1) is 0
                double against = (Math.Log(Math.Max(2, monsterGuildLevel)) * 4)
                               * (Math.Log(dungeonLevel + 1) * 1.25) * 1.25;
                double r = (stat + (4.5 + Math.Exp(Math.Log(lvl + 1) / Math.Log(15))) * (guildRating / 10 * 14))
                         - against * 1.5;
                return dungeonLevel > 0 ? Math.Clamp(r, 5, 98) : r;
            }
            case 4:     // fighting. mc0080 is 0.6, not zero - MODULE6.BAS:8
                return FightingBase + (((Math.Log(100 + lvl) / 1.75) - 2.3) * (guildRating * guildRating)) / 2;
            case 5:     // perception
            {
                // note the original's precedence here differs from the thieving case: it is
                // WIS + gearWIS/4, not (WIS + gearWIS)/4
                double stat = wis;
                double climb = Math.Log(lvl) / Math.Log(1.25);
                double r = (guildRating + climb) * (guildRating * 0.3) + stat;
                return Math.Clamp(r - dungeonLevel * 2, 5, 90);
            }
            default:    // backstab, critical hit, extra swings
                return guildRating * lvl;
        }
    }

    /// <summary>
    /// <c>abilityChance(c, ability, True)</c> (MAINMOD2.BAS:1918-1985) as the Char page calls it: every
    /// guild the character belongs to, in table order, the best chance above nought winning and its
    /// guild and modifier left in <c>gv141C</c>. With the third argument True the thieving case takes
    /// its unclamped branch against a guild-level-2 creature at the character's own depth. No guild
    /// beats nought and the modifier stays 0.
    /// </summary>
    public static (double Chance, string Guild, double Modifier) AbilityLead(Character c, int ability, Func<short, Item?>? gear = null)
    {
        var (wis, dex) = AbilityStats(c, ability, gear);
        double best = 0, mod = 0; string guild = "";
        foreach (var g in GuildData.All)
        {
            var m = c.Guilds.FirstOrDefault(x => x.Guild == g.Name);
            if (m == null || m.Level <= 0) continue;
            double gm = g.Abilities.Length > ability ? g.Abilities[ability] : 0;
            double r = ability == 0
                ? AbilityChance(0, gm, m.Level, wis, dex, 0, 2)
                  - (Math.Log(2) * 4) * (Math.Log(c.DungeonLevel + 1) * 1.25) * 1.25 * 1.5
                : AbilityChance(ability, gm, m.Level, wis, dex, c.DungeonLevel, 2);
            if (r > best) { best = r; mod = gm; guild = g.Name; }
        }
        return (best, guild, mod);
    }

    /// <summary>The best chance any of the character's guilds gives for an ability.</summary>
    public static double BestAbilityChance(Character c, int ability, int dungeonLevel, int monsterGuildLevel = 2, Func<short, Item?>? gear = null)
    {
        var (wis, dex) = AbilityStats(c, ability, gear);
        double best = 0;
        foreach (var g in GuildData.All)
        {
            var m = c.Guilds.FirstOrDefault(x => x.Guild == g.Name);
            if (m == null || m.Level <= 0) continue;
            double r = AbilityChance(ability, g.Abilities.Length > ability ? g.Abilities[ability] : 0,
                                     m.Level, wis, dex, dungeonLevel, monsterGuildLevel);
            if (r > best) best = r;
        }
        return best;
    }

    // ------------------------------------------------------------------ chest traps

    /// <summary>The trap table, gv08AA in MODULE1.BAS. Index 0 means the chest is not trapped.</summary>
    public static readonly string[] TrapNames =
    {
        "Not Trapped", "Poison Gas", "Disease Spores", "Exploding Trap", "Fire Trap",
        "Slime", "Fate", "Teleporter", "Blackout", "Fear", "Withering",
    };

    /// <summary>
    /// Which trap a chest carries. The generator only rolls one at all when the chest class is
    /// above zero, with a 25 / 75 / 90 percent chance by class, and then keeps drawing an index
    /// from 0 to 11 until it finds one the monster's TrapFlags actually allows.
    /// </summary>
    public static int ChestTrap(Monster m, int chestClass, Random rng)
    {
        if (chestClass <= 0 || m.TrapFlags == 0) return 0;
        int chance = chestClass switch { 1 => 25, 2 => 75, 3 => 90, _ => 0 };
        if (rng.Next(100) + 1 >= chance) return 0;
        for (int guard = 0; guard < 200; guard++)
        {
            int t = rng.Next(12);
            if ((m.TrapFlags & (1 << t)) != 0) return t;
        }
        return 0;
    }

    /// <summary>
    /// The morgue's chance of a successful raise, <c>fn142D</c> in MAINMOD2.BAS called with -1 for
    /// a character raising themselves. With no resurrection spell already on them the spell term
    /// works out at a flat 40, so it is <c>35 * (CON / 4.5) + 40</c>, clamped to 10..98 - which is
    /// why the help calls the morgue workers "very efficient".
    /// </summary>
    public static int MorgueRaiseChance(int con) =>
        Math.Clamp((int)Math.Round(35 * (con / 4.5) + 40, MidpointRounding.ToEven), 10, 98);

    /// <summary>
    /// How someone died, which decides what can bring them back (<c>M5483</c>). The two that
    /// matter are the two <c>fn142D</c> tests for; everything else is an ordinary death.
    /// </summary>
    public const int DeathOrdinary = 0, DeathStoned = 2, DeathRock = 8;

    /// <summary>
    /// Whether a raise spell will work on this body at all, <c>fn142D</c>'s first half
    /// (MODULE6.BAS:2743). Stone yields only to Restore Flesh, and someone pulled into rock only
    /// to Resurrect - and the morgue cannot help them at all. Returns the refusal, or null.
    /// </summary>
    public static string? RaiseRefusal(int causeOfDeath, int effect)
    {
        const int restoreFlesh = 21, resurrect = 22;
        switch (causeOfDeath)
        {
            case DeathStoned:
                return effect != restoreFlesh && effect > -1 ? "Character is stoned!  This Spell will not work!" : null;
            case DeathRock:
                if (effect == -1) return "The morgue cannot help characters pulled from Rock.";
                return effect != resurrect ? "Character is Rock!  This spell will not work!" : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// A raise spell's chance, <c>fn142D</c>'s second half: <c>35 * (CON / 4.5) + tier * 10</c>
    /// where the tier counts up from Raise Dead (effect 20) as one, clamped to 10..98. Being
    /// pulled from rock overrides all of it and leaves a flat 10.
    /// </summary>
    public static int SpellRaiseChance(int con, int effect, int causeOfDeath)
    {
        int tier = Math.Max(1, effect - 19);
        // l0456 is an Integer (MAINMOD2.BAS:2759), so the Double is rounded half to even on the way
        // in, not truncated: CON 13 is 101.1 and CON 5 is 38.89, which the port used to make 38
        int chance = Math.Clamp((int)Math.Round(35 * (con / 4.5) + tier * 10, MidpointRounding.ToEven), 10, 98);
        return causeOfDeath == DeathRock ? 10 : chance;
    }

    /// <summary>
    /// What poison and disease do on one step through the dungeon, from FRMDUNM.BAS. Each is a cure
    /// roll first, and costs hits if the cure fails:
    /// <code>
    ///   If Ailments(0) > 0 Then                                   ' poisoned
    ///       l013C = Int(Rnd * 200) + ((CON + gearCON - 10) * 2) - (Ailments(0) * 2)
    ///       If l013C > 185 Then cured Else Hits = Hits - 2
    ///   If Ailments(1) Then                                       ' diseased
    ///       l013C = Int(Rnd * 200) + ((CON + gearCON - 10) * 2) - (Ailments(0) * 2)
    ///       If l013C > 195 Then cured Else Hits = Hits - 1
    /// </code>
    /// Disease is harder to shake and hurts less. Note that the disease roll subtracts
    /// <c>Ailments(0)</c> - the *poison* level - in the original, not the disease level. That looks
    /// like a copy-paste slip, but it is what the game computes, so it is what this computes: being
    /// poisoned as well makes a disease harder to throw off, while the disease's own strength never
    /// matters at all.
    /// </summary>
    public static (bool CuredPoison, bool CuredDisease, int HitsLost) AilmentStep(
        Random rng, int effectiveCon, int poisonLevel, int diseaseLevel)
    {
        bool curedP = false, curedD = false;
        int lost = 0;
        if (poisonLevel > 0)
        {
            int roll = rng.Next(200) + (effectiveCon - 10) * 2 - poisonLevel * 2;
            if (roll > 185) curedP = true; else lost += 2;
        }
        if (diseaseLevel > 0)
        {
            // Ailments(0) is read again here, so on a tick that just cured the poison it is 0 (FRMDUNM.BAS:538-547)
            int roll = rng.Next(200) + (effectiveCon - 10) * 2 - (curedP ? 0 : poisonLevel) * 2;
            if (roll > 195) curedD = true; else lost += 1;
        }
        return (curedP, curedD, lost);
    }

    /// <summary>
    /// What the chest panel shows before you touch it, <c>monShowChest</c> (MONSMOD.BAS:4124-4176).
    ///
    /// Two separate things are on display. The trap *name* is a guess: your wisdom against the
    /// guarding monster's level and dexterity, plus your thieving ability and the square of the log
    /// of your thieving guild's level. Beat a 1..100 roll and you are shown the real trap; fail and
    /// you are shown one of the eleven picked at random, with nothing to tell you which happened.
    /// The diamonds after it are how sure you are - one to four - and they come from the same
    /// figure, so a confident reading and a correct one go together but are not the same thing.
    ///
    /// The <c>+</c>, <c>*</c> or <c>-</c> before the diamonds is your chance of *disarming* it,
    /// which is a different question from knowing what it is: plus above 55, star between 45 and
    /// 55, minus below 45, one to three of them.
    ///
    /// The original computes a second figure into <c>l0636</c> at MONSMOD.BAS:4135 and overwrites
    /// it on the very next line with the plain thieving ability, so that whole expression - and the
    /// <c>l0604</c> above it - is dead. Only the surviving value is reproduced.
    /// </summary>
    public static (string Gauge, int Diamonds, int ShownTrap) TrapReadout(
        Random rng, int naturalWis, int gearWis, int monsterGuildLevel, int monsterDex,
        double thievingChance, int thievingGuildLevel, int realTrap)
    {
        // note the original's precedence: only the gear part is quartered
        double confidence = naturalWis + gearWis / 4.0;
        confidence -= Math.Log(Math.Max(1, monsterGuildLevel)) * Math.Log(Math.Max(1, monsterDex)) * 2.5;

        double disarm = thievingChance;
        if (disarm < 5) disarm = 5;
        if (confidence < 2) confidence = 2;
        if (confidence > 98) confidence = 98;

        double guildTerm = Math.Log(Math.Max(1, thievingGuildLevel));
        confidence = Math.Floor(confidence + disarm / 1.2) + guildTerm * guildTerm;
        if (disarm > 98) disarm = 98;

        int shown = confidence < rng.Next(100) + 1 ? rng.Next(ChestTraps.Names.Length) : realTrap;

        if (confidence > 100) confidence = 100;
        if (confidence < 1) confidence = 1;
        int diamonds = (int)(confidence / 10 / 2.3);
        if (diamonds < 1) diamonds = 1;

        char sign;
        int count;
        if (disarm is > 44 and < 56) { sign = '*'; count = 1; }
        else if (disarm < 45) { sign = '-'; count = (int)(5 - disarm / 15 - 0.5); }
        else { sign = '+'; count = (int)((disarm - 50) / 15); }
        if (count < 1) count = 1;
        if (count > 3) count = 3;

        return (new string(sign, count), diamonds, shown);
    }

    /// <summary>
    /// Whether the chest will not open at all. <c>gv1D7E</c> is never assigned anywhere in the
    /// program, so it is zero, and the test at MONSMOD.BAS:4128 comes to "lock state at or below
    /// zero". A chest the encounter locked carries 1 and so shows nothing but "Magically Locked";
    /// one a Charm of Opening has since unlocked carries -1 and reads normally. The help agrees:
    /// "If the chest is Magically Locked or Unidentified, nothing will happen."
    /// </summary>
    public static bool MagicallyLocked(int lockState) => lockState > 0;

    public const string LockedCaption = "Magically Locked";

    /// <summary>
    /// The chance of realising you had the wrong name for something, FRMDUNM.BAS:475-489. A
    /// separate roll from the one that lifts fog: this one is your identify skill plus 20, cut to
    /// three quarters *in* fog rather than raised outside it, and clamped 10 to 90.
    /// </summary>
    public static double RenameChance(double identifySkill, bool inFog)
    {
        double p = (inFog ? identifySkill * 0.75 : identifySkill) + 20;
        if (p < 10) p = 10;
        if (p > 90) p = 90;
        return p;
    }


    /// <summary>
    /// A draw of <c>Rnd * n</c> assigned straight into a VB Integer, as the store and the pens both
    /// make it (MAINMOD.BAS:1337 and :1598). The assignment rounds half to even, which is not the
    /// uniform 0..n-1 it looks like:
    ///
    ///   index 0 owns only Rnd * n below 0.5, so it comes up half as often as any other;
    ///   index n is reachable and matches nothing, so that draw is spent and taken again.
    ///
    /// Both are the original's, and both are easy to erase by reaching for a plain random index.
    /// </summary>
    public static int RoundedIndex(Random rng, int n) =>
        (int)Math.Round(rng.NextDouble() * n, MidpointRounding.ToEven);

    // ------------------------------------------------------------------ quests

    /// <summary>
    /// The depth a quest target is drawn from: <c>CInt(level / 25)</c> clamped to 1..10
    /// (FRMCTYG.BAS:471-477). The community's page says "between Level 1 and character's current
    /// guild / 25, rounded off -- maximum Level 10".
    /// </summary>
    public static int QuestTier(int guildLevel) =>
        Math.Clamp((int)Math.Round(guildLevel / 25.0, MidpointRounding.ToEven), 1, 10);

    /// <summary>
    /// An item quest squeezes that tier again - <c>l0110 = Int(l0110 / 1.5#) + 1</c>,
    /// FRMCTYG.BAS:524 - so the deepest item anyone is ever sent for is level 7, which is what the
    /// page means by "you will never be quested for items from below Level 7".
    /// </summary>
    public static int QuestItemBand(int tier) => (int)(tier / 1.5) + 1;

    /// <summary>
    /// The quest the guild hands out with a new level, from FRMCTYG.BAS:
    /// <code>
    ///   l010E = Rnd * 100
    ///   If level &lt; 5 Then l010E = 100                  ' never below level 5
    ///   clear the current quest
    ///   If l010E > GuildDefs(guild).QuestPercent Then Exit
    ///   "Questmaster is going through his books..."
    ///   tier = CInt(level / 25), clamped 1..10
    ///   If Rnd * 100 &lt;= 5 Then "Questmaster lets you off the hook." : Exit
    ///   If Rnd * 100 &lt; 25 And level > 15 Then type = fetch Else type = defeat
    /// </code>
    /// Returns 0 for no quest, -1 for let off the hook (the books were looked through, and that is
    /// said), 1 to defeat a monster, 2 to fetch an item, along with the tier the
    /// target is drawn from.
    /// </summary>
    public static (int Type, int Tier) RollQuest(GuildDef guild, int guildLevel, Random rng)
    {
        // `Dim l010E As Integer` (FRMCTYG.BAS:211), so every `l010E = Rnd * 100` rounds on the way
        // in - half to even, as VB does - and the three tests below compare whole numbers. It is a
        // small thing that moves all three probabilities half a point, and it is why the community's
        // Questing_Formula page quotes a 6% let-off and a 1% floor: its author saw the integer and
        // assumed truncation. Truncation would give exactly those; rounding gives 5.5% and 0.5%.
        //
        // The same page's 74% for the rare-monster re-roll is the same assumption applied where it
        // does not hold at all - FRMCTYG.BAS:517 tests `((Rnd * 100) > 25)` in the expression, with
        // nothing to round it, so that one really is 75%. See QuestMonster.
        int Roll() => (int)Math.Round(rng.NextDouble() * 100, MidpointRounding.ToEven);

        int roll = guildLevel < 5 ? 100 : Roll();
        if (roll > guild.QuestPercent) return (0, 0);

        int tier = QuestTier(guildLevel);
        if (Roll() <= 5) return (-1, tier);                                   // let off the hook: -1, which is said
        int type = Roll() < 25 && guildLevel > 15 ? 2 : 1;
        return (type, tier);
    }

    /// <summary>
    /// The monster a "defeat one" quest names - FRMCTYG.BAS:489-521, line for line and draw for draw:
    /// <code>
    ///   Do
    ///     cls = Int(Rnd * NumMonsterClasses)
    ///     If Int(Rnd * 100) &lt; 40 Then start = gv1DD2(cls, 0) Else start = -1
    ///     last = -1
    ///     For i = gv1DD2(cls, 0) To gv1DD2(cls, 1)
    ///       If gv1DD2(cls, 0) = -1 Then Exit For
    ///       If start = -1 Then If Monsters(i).Floor >= tier Then start = i
    ///       If Monsters(i).Floor > tier Or i = gv1DD2(cls, 1) Then last = i - 1 : Exit For
    ///     Next
    ///     If start = -1 Then start = gv1DD2(cls, 0)
    ///     pick = CInt(Rnd * (last - start)) + start
    ///     If Monsters(pick).Rarity &lt; 10 And ((Rnd * 100) > 25) Then pick = -1
    ///   Loop While last = -1 Or Monsters(last).Floor > tier Or pick = -1
    /// </code>
    /// What that does, all of it kept:
    /// <list type="bullet">
    /// <item>three times in five the run starts at the first monster of the class found <b>at or
    /// below</b> the tier, so the draw leans to monsters of the quest's own level; otherwise at
    /// the head of the class;</item>
    /// <item>the run ends one before the first monster deeper than the tier - or one before the
    /// class's <b>last</b> monster, which is therefore never named;</item>
    /// <item>when the first monster at the tier is already deeper than it, the run ends one
    /// <b>before</b> its start, and <c>CInt</c> of a negative span can step back one record - into
    /// the class before, or record 0, GOBLIE - and that is accepted if the record before is shallow
    /// enough and not turned down as rare;</item>
    /// <item>VB's <c>And</c> does not short-circuit: the <c>Rnd * 100</c> is drawn on every pass,
    /// rare monster or not. Chance is the Rarity field. With nothing to round it the test really is
    /// three times in four; the community page's 74% assumes an integer that is not there;</item>
    /// <item>no cap: it loops until something is valid. The shipped data always gets there.</item>
    /// </list>
    /// The class table is <c>gv1DD2</c>, <see cref="EncounterGenerator.ClassRanges"/>.
    /// </summary>
    public static Monster? QuestMonster(IReadOnlyList<Monster> all, int tier, Random rng)
    {
        int nc = EncounterGenerator.ClassCount(all);
        if (nc <= 0 || all.Count == 0) return null;
        var r = EncounterGenerator.ClassRanges(all);
        // the original has no way out; this only guards a table with no class of monsters in it,
        // where its loop could never end (an empty class is a subscript error at Monsters(-1)).
        // The shipped data fills every class (Combat.cs ClassRanges)
        bool anyClass = false;
        for (int c = 0; c < nc; c++) if (r[c, 0] != -1) anyClass = true;
        if (!anyClass) return null;

        int pick, last;
        do
        {
            int cls = (int)Math.Floor(rng.NextDouble() * nc);                                   // :491
            int start = Math.Floor(rng.NextDouble() * 100) < 40 ? r[cls, 0] : -1;               // :492-496
            last = -1;                                                                           // :497
            if (r[cls, 0] != -1)                                                                 // :499
                for (int i = r[cls, 0]; i <= r[cls, 1]; i++)                                     // :498
                {
                    if (start == -1 && all[i].LevelFound >= tier) start = i;                     // :502-506
                    if (all[i].LevelFound > tier || i == r[cls, 1]) { last = i - 1; break; }     // :507-510
                }
            if (start == -1) start = r[cls, 0];                                                  // :512-514
            double span = rng.NextDouble() * (last - start);                                     // :515
            // an empty class: the original's Monsters(-1) at :517 is a subscript error (see above)
            if (r[cls, 0] == -1) { pick = -1; continue; }
            pick = (int)Math.Round(span, MidpointRounding.ToEven) + start;                       // :515-516
            bool rare = all[pick].Chance < 10;
            bool turned = rng.NextDouble() * 100 > 25;                                           // :517, drawn always
            if (rare && turned) pick = -1;
        }
        while (last == -1 || all[last].LevelFound > tier || pick == -1);                          // :520
        return all[pick];
    }

    /// <summary>
    /// Whether <see cref="QuestItem"/>'s loop can end: for some kind whose first item in the file is
    /// within the band, one of the picks its run allows - first + 1 to last, or first + 1 alone for a
    /// run of one - is a real record worth 100 or more, not cursed and not from level 0. The rare
    /// test then passes a quarter of the time, so any such pick ends it.
    /// </summary>
    public static bool QuestItemReachable(IReadOnlyList<Item> all, int band, int kinds)
    {
        int n = all.Count;
        for (int k = 0; k < kinds; k++)
        {
            int first = -1;
            for (int i = 0; i < n; i++) if (all[i].Type == k) { first = i; break; }
            if (first < 0 || all[first].Floor > band) continue;
            int last = -1;
            for (int i = first; i < n; i++)
                if (all[i].Floor > band || i == n - 1 || all[i].Type != k) { last = i - 1; break; }
            if (last < first) last = first;
            int top = last == first ? first + 1 : last;
            for (int p = first + 1; p <= top; p++)
                if (p < n && all[p].Price >= 100 && all[p].Cursed == 0 && all[p].Floor >= 1) return true;
        }
        return false;
    }

    /// <summary>
    /// The item a "bring one back" quest names - FRMCTYG.BAS:523-561, line for line and draw for
    /// draw. The tier is squeezed first, <c>Int(tier / 1.5) + 1</c> (:524), then:
    /// <code>
    ///   Do
    ///     Do : Do
    ///       first = -1 : kind = Int(Rnd * NumItemTypes)
    ///       first = the first item in the file of that kind
    ///     Loop While first = -1 : Loop While Items(first).Floor > band
    ///     last = one before the first item from first on that is deeper than the band, or of
    ///            another kind, or the last record of the file
    ///     If last &lt; first Then last = first
    ///     pick = Int(Rnd * (last - first) + 1) + first
    ///     reject: Price &lt; 100, Cursed, Floor &lt; 1, Rarity &lt; 5 And (Rnd * 100) > 25
    ///   Loop While pick = -1
    /// </code>
    /// The <c>+ 1</c> sits inside the <c>Int</c>, so <c>Int(Rnd * span + 1)</c> is 1 to span and the
    /// pick runs from <b>first + 1</b> to <b>last</b>: the run's own first item is never named. A run
    /// of one item has a span of 0, and then the pick is <b>first + 1, one past the run's end</b> -
    /// the next kind's first item, or one of this kind deeper than the band - which is named if it
    /// passes the tests below. The run also stops one short of the file's last record. The type's first
    /// item must itself be within the band, or the kind is drawn again. Item 0 is the null record,
    /// and the file's one past the end is the blank record <c>ReDim Items(gv1BEC)</c> leaves
    /// (MODULE1.BAS:730), which is worth 0 and so is turned down. The rejections are an
    /// <c>ElseIf</c> chain, so the rare test's <c>Rnd</c> is drawn - whatever the Rarity, since
    /// <c>And</c> does not short-circuit - only for an item that passed the first three. Price
    /// under 100, cursed and level 0 are what keep the guild crests out. No cap: it loops until
    /// something is valid.
    /// </summary>
    public static Item? QuestItem(IReadOnlyList<Item> all, int tier, Random rng)
    {
        int band = QuestItemBand(tier);
        int n = all.Count;
        int kinds = Mdata1.ItemTypes.Count > 0 ? Mdata1.ItemTypes.Count : n == 0 ? 0 : all.Max(i => (int)i.Type) + 1;
        // only a table where no pick can pass could spin for ever; the original would hang there.
        // Nothing in the shipped data does (QuestItemReachable)
        if (kinds == 0 || !QuestItemReachable(all, band, kinds)) return null;

        int pick;
        do
        {
            int first, kind;
            do
            {
                do
                {
                    first = -1;                                                                  // :528
                    kind = (int)Math.Floor(rng.NextDouble() * kinds);                            // :529
                    for (int i = 0; i < n; i++)                                                  // :530-535
                        if (all[i].Type == kind) { first = i; break; }
                }
                while (first == -1);                                                             // :536
            }
            while (all[first].Floor > band);                                                     // :537

            int last = -1;                                                                       // :538-546: the
            for (int i = first; i < n; i++)                                                      // Do around it
                if (all[i].Floor > band || i == n - 1 || all[i].Type != kind) { last = i - 1; break; }
            if (last < first) last = first;                                                      // :547-549

            pick = (int)Math.Floor(rng.NextDouble() * (last - first) + 1) + first;               // :550-551
            Item? it = pick < n ? all[pick] : null;                                              // Items(gv1BEC): blank
            if (it == null || it.Price < 100) pick = -1;                                         // :552
            else if (it.Cursed != 0) pick = -1;                                                  // :554
            else if (it.Floor < 1) pick = -1;                                                    // :556
            else
            {
                bool rare = it.Rarity < 5;
                bool turned = rng.NextDouble() * 100 > 25;                                       // :558
                if (rare && turned) pick = -1;
            }
        }
        while (pick == -1);                                                                      // :561
        return all[pick];
    }


    /// <summary>
    /// The gate on a couple of the nastier specials: 50 percent normally, 90 if your guild level is
    /// more than a quarter above the monster's, 10 if it is more than a quarter below.
    /// <para>
    /// Note the direction: being the *stronger* party makes these more likely to land on you, not
    /// less. That reads backwards, like the chest-trap resistance test, but it is what the original
    /// computes and it is reproduced here. See docs/FIDELITY.md.
    /// </para>
    /// </summary>
    public static int LevelGapChance(int charGuildLevel, int monsterGuildLevel)
    {
        if (charGuildLevel > monsterGuildLevel + monsterGuildLevel / 4.0) return 90;
        if (charGuildLevel < monsterGuildLevel - monsterGuildLevel / 4.0) return 10;
        return 50;
    }
}
