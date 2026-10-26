using Mordor.Core;

/// <summary>
/// Why fn0D4D (MONSMOD.BAS:1361-1989) has no retry cap in this port: its two unbounded loops end,
/// with probability 1, for the shipped MDATA5 and MDATA11. This works it out from the data, so a
/// change to either file that would let the game hang shows up as a failed check.
///
/// **The like-types loop** (L2003C, MONSMOD.BAS:1806-1853). A companion draw that picks nobody
/// goes round again with a fresh roll. It walks the window from the top, and once the running
/// total passes the roll every index below qualifies too - except the lead, which is skipped. So a
/// state picks nobody on every roll exactly when the only weighted index is the lead and nothing
/// but the lead lies between the window's start and it. That needs the lead to carry weight, which
/// it cannot on the first companion (it is the previous group's monster, and excluded), so the
/// walk follows every chain a lead can start - the picks from a state taken generously, every
/// index but the lead from the window's start to its end - and looks for such a state.
///
/// **The restart loop** (L1EB02). Everything that fails goes back to the top with fresh rolls, so
/// it ends as long as every state it can restart in has some way through. The only thing that
/// survives a restart is the target floor m0282, which the group loop can lower (a companion is
/// never deeper than the group before it) or raise to a lair monster's floor. So for every area,
/// with and without a stud, and every target from 1 to the deepest the area can reach, this looks
/// for one path with a chance above nought: the lair, or a class the pick can land on (the 1% roll
/// reaches every class unless it is a newbie room; the weighted pick reaches a class only if the
/// class before it has weight, which is the v(0) fault), a spread the routine can arrive at, a lead
/// in that window that passes the newbie guard, and - for a lead of four groups, which cannot come
/// out as one - a first companion.
/// </summary>
public static class EncounterLoopsProbe
{
    public static (int LikeTypeHangs, int DeadEnds, int BadCompanions) Analyse(string dir)
    {
        var all = MdrLoader.LoadMonsters(System.IO.Path.Combine(dir, "MDATA5.MDR"));
        var dungeon = MdrLoader.LoadDungeon(System.IO.Path.Combine(dir, "MDATA11.MDR"));
        int nc = EncounterGenerator.ClassCount(all), n = all.Count;
        var R = EncounterGenerator.ClassRanges(all);

        // data the generator can only meet as an error (see Generate): a companion class outside
        // the table, or a direct spawn mode naming an index past the end
        int bad = 0;
        for (int i = 1; i < n; i++)
        {
            var m = all[i];
            bool badClass = m.CompanionType < 0 || m.CompanionType >= nc;
            bool badDirect = m.CompanionSpawnMode > 7 && m.CompanionSpawnMode - 7 >= n;
            if (badClass || badDirect) { bad++; Console.WriteLine($"  bad companion data: {i} {m.Name} type {m.CompanionType} mode {m.CompanionSpawnMode}"); }
        }

        // one companion step from the state a monster's companion fields set (1739-1847)
        (List<int> Picks, bool Hang, bool Direct) Step(int lead, int prev)
        {
            var p = all[prev];
            int mode = p.CompanionSpawnMode;
            if (mode >= 3 && mode != 7) return (new List<int>(), false, true);
            if (mode < 0 || mode == 7) return (new List<int>(), true, false);   // the stale window: not in the data
            int below = mode == 2 ? 0 : 3, above = mode == 0 ? -1 : 0, cur = p.LevelFound, sub = p.CompanionSubtype;
            if (cur + above < 1) above = 0;
            int ct = p.CompanionType, start = -1, end = ct >= 0 && ct < nc ? R[ct, 1] : -1;
            var weighted = new List<int>();
            for (int i = ct >= 0 && ct < nc ? R[ct, 0] : -1; i >= 0 && i <= R[ct, 1]; i++)
            {
                if (start < 0) start = i;
                var m = all[i];
                if (m.LevelFound < cur - below) { }
                else if (m.LevelFound > cur + above) { end = i - 1; break; }
                else if (m.InGroup == 1) { }
                else if (i != prev && ((sub > 0 && m.Subtype == sub) || sub == 0) && m.Chance > 0) weighted.Add(i);
            }
            if (weighted.Count == 0) return (new List<int>(), false, false);    // W = 0: a restart
            int h = weighted.Max();
            bool any = false;
            for (int i = start; i <= h; i++) if (i != lead) any = true;
            var picks = new List<int>();
            for (int i = start; i <= Math.Max(end, h); i++) if (i != lead) picks.Add(i);
            return (picks, !any, false);
        }

        int hangs = 0;
        for (int lead = 1; lead < n; lead++)
        {
            int most = Math.Clamp((int)all[lead].NumGroups, 1, 4);
            var states = new HashSet<int> { lead };
            for (int g = 1; g < most; g++)
            {
                var next = new HashSet<int>();
                foreach (int prev in states)
                {
                    var (picks, hang, direct) = Step(lead, prev);
                    if (hang) hangs++;
                    if (direct) next.Add(prev);          // no chaining: the state stands
                    else next.UnionWith(picks);
                }
                states = next;
            }
        }

        // a lead the group loop can see through: one group is possible below four, else the first
        // companion must exist
        bool Through(int lead)
        {
            var l = all[lead];
            if (l.NumGroups <= 3) return true;
            var (picks, hang, direct) = Step(lead, lead);
            return direct || (picks.Count > 0 && !hang);
        }
        List<int> Leads(int c, int t, int s)
        {
            var res = new List<int>();
            if (c < 0 || c >= nc || R[c, 0] < 0) return res;
            int start = R[c, 0], end = R[c, 1];
            for (int i = R[c, 0]; i <= R[c, 1]; i++)
                if (all[i].LevelFound > t) { end = Math.Max(i - 1, start); break; }
            for (int i = start; i <= end; i++)
                if (all[i].LevelFound >= t - s && all[i].LevelFound <= t && all[i].Chance > 0) res.Add(i);
            return res;
        }
        int W(int c, int t, int s) => Leads(c, t, s).Sum(i => (int)all[i].Chance);

        int deadEnds = 0;
        for (int lv = 1; lv <= dungeon.Count; lv++)
        {
            var f = dungeon[lv - 1];
            var seen = new HashSet<(int, bool)>();
            for (int y = 0; y < f.Height; y++)
                for (int x = 0; x < f.Width; x++)
                {
                    int id = f.AreaIdAt(x, y);
                    if (id <= 0 || f.Has(x, y, Floor.ROCK)) continue;
                    bool stud = f.Has(x, y, Floor.STUD);
                    if (!seen.Add((id, stud))) continue;
                    var a = f.Areas[id];
                    if (a.SpawnMask == 0) continue;
                    bool newbie = lv == 1 && id < 18;
                    int lair = a.LairId > 0 ? all.FindIndex(m => m.Id == a.LairId) : -1;
                    var list = new List<int>();
                    for (int c = 0; c < nc; c++)
                        if (((a.SpawnMask >> c) & 1) != 0 && R[c, 0] != -1) list.Add(c);
                    var spreads = new HashSet<int>
                    {
                        Math.Min((int)Math.Round(lv / 5.0, MidpointRounding.ToEven), lv),
                        Math.Min((int)Math.Round(1 + lv / 5.0, MidpointRounding.ToEven), lv),
                    };
                    if (stud) spreads.Add(Math.Min(1, lv));
                    int deepest = Math.Max(stud ? lv + 1 : lv, lair >= 0 ? all[lair].LevelFound : 0);
                    for (int t = 1; t <= deepest; t++)
                    {
                        bool ok = lair >= 0 && Through(lair);
                        foreach (int s0 in spreads)
                        {
                            if (ok) break;
                            var classes = new HashSet<int>();
                            if (!newbie || list.Count == 0) for (int c = 0; c < nc; c++) classes.Add(c);
                            if (list.Count > 0)
                            {
                                var w = list.Select(c => (double)W(c, t, s0)).ToList();
                                double total = w.Sum();
                                if (total == 0) for (int k = 0; k <= nc - 2; k++) classes.Add(k < list.Count ? list[k] : 0);
                                else
                                {
                                    double thr = w[0];
                                    for (int k = 0; k < list.Count; k++)
                                    {
                                        double before = k == 0 ? 0 : thr - w[k - 1];
                                        if (thr > before && before / total * 100 < 101) classes.Add(list[k]);
                                        thr += w[k];
                                    }
                                    classes.Add(list[^1]);
                                }
                            }
                            foreach (int c in classes)
                            {
                                int s = s0;
                                while (W(c, t, s) == 0 && s + 1 <= 3) s++;
                                foreach (int lead in Leads(c, t, s))
                                {
                                    if (newbie && all[lead].GuildLevel > 2 && lead != 4) continue;
                                    if (Through(lead)) { ok = true; break; }
                                }
                                if (ok) break;
                            }
                        }
                        if (!ok) { deadEnds++; Console.WriteLine($"  no way through: level {lv} area {id} stud {stud} target {t}"); }
                    }
                }
        }
        return (hangs, deadEnds, bad);
    }
}
