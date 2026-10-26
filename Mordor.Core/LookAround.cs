using System.Globalization;

namespace Mordor.Core;

/// <summary>
/// <c>mdrmazLookAround</c> (MAZEMOD.BAS:255-346): who is in the room, for the Look page and for the
/// count on the end of the dungeon's position readout.
///
/// It walks the whole roster (<c>MorgueList</c>, every character there is) and takes each one
/// on the party leader's floor and in the leader's area (<c>M59BB</c>, the area number of the
/// square they stand in; -1 in town), or the leader themselves. A dead one who is not the leader
/// is counted apart from the rest. Nothing else is looked at - not the Hide option, which only
/// stops a character being picked up.
///
/// On the Look page each one is a row under four bold headings - Name at column 1, Guild (the
/// current one) at 9, Level at 15, Hits at 19 - in plain type, dead or alive. The readout gets
/// "[ n ]", or "[ n+d ]" when anyone dead is there.
///
/// The roster's entries are what was last saved, except the character on screen, whose entry is
/// refreshed before the walk (MAZEMOD.BAS:278-285). The port has no roster file: the party comes
/// from memory and everyone else from their saves, in the roster's record order (<see cref="Roster"/>).
/// </summary>
public static class LookAround
{
    /// <summary>One roster entry as the walk reads it.</summary>
    public readonly record struct Entry(string Name, string Guild, int Level, int Hits, bool CountedDead);

    /// <summary>
    /// The walk. <paramref name="roster"/> is every character, <paramref name="placeOf"/> the floor
    /// and the <c>M59BB</c> of one (-1 in town). <paramref name="everywhere"/> is mode 3, which takes the
    /// whole roster regardless of place.
    /// </summary>
    public static (List<Entry> Here, int Living, int Dead) Walk(Character leader, IEnumerable<Character> roster,
                                                              Func<Character, (int Level, int Area)> placeOf, bool everywhere = false,
                                                              Func<Character, Listed?>? listed = null)
    {
        var here = new List<Entry>();
        int living = 0, dead = 0;
        var (leaderLevel, leaderArea) = placeOf(leader);
        foreach (var r in roster)
        {
            bool isLeader = string.Equals(r.Name.Trim(), leader.Name.Trim(), StringComparison.OrdinalIgnoreCase);
            var (level, area) = placeOf(r);
            if (!everywhere && (level != leaderLevel || (area != leaderArea && !isLeader))) continue;
            var l = listed?.Invoke(r);
            bool countedDead = (l?.Dead ?? r.IsDead) && !isLeader;
            if (countedDead) dead++; else living++;
            here.Add(new Entry(r.Name.TrimEnd(), r.Guild.Guild, r.Guild.Level, l?.Hits ?? r.CurHits, countedDead));
        }
        return (here, living, dead);
    }

    /// <summary>
    /// A party member's <c>MorgueList</c> Hits and DeathState as <c>sub0956</c> last wrote them
    /// (MULTIMOD.BAS:724-731) - see <see cref="ListedParty"/>.
    /// </summary>
    public readonly record struct Listed(int Hits, bool Dead);

    /// <summary>
    /// The staleness of <c>MorgueList</c> for the party. The walk reads the list, not the records:
    /// <c>sub0956</c> copies each member's Hits and DeathState into it (MULTIMOD.BAS:724-731) only
    /// when more than one is loaded, and it runs from <c>mdrmazShowMaze</c> (MAZEMOD.BAS:832), the
    /// party list's prune (FRMGROUP.BAS:342), a teleport and a chute - not from a combat round, the
    /// ailment clock or the end of a fight (sub0C53's mdrmazLookAround, MONSMOD.BAS:791). The walk
    /// itself refreshes only <c>gv1A82</c>'s entry, and not its DeathState (MAZEMOD.BAS:279-285). So
    /// the Look page shows the other members' hits as the last view redraw left them.
    /// </summary>
    public sealed class ListedParty
    {
        private readonly Dictionary<Character, Listed> _rows = new();

        /// <summary><c>sub0956</c>'s copy (MULTIMOD.BAS:713-731): nothing for a lone character.</summary>
        public void Sync(IReadOnlyList<Character> party)
        {
            if (party.Count <= 1) return;
            foreach (var m in party) _rows[m] = new Listed(m.CurHits, m.IsDead);
        }

        /// <summary>
        /// What the walk reads for <paramref name="c"/>: the live record for the one the windows show
        /// (its Hits refreshed, MAZEMOD.BAS:280) and for anyone not in the copy, the copy otherwise.
        /// </summary>
        public Listed? For(Character c, Character? shown)
        {
            if (!_rows.TryGetValue(c, out var l)) return null;
            return c == shown ? l with { Hits = c.CurHits } : l;
        }
    }

    /// <summary>
    /// The roster as the port can have it: every saved character in record order, with the party's
    /// own in-memory records standing in for their saves, and any party member not saved yet last.
    /// </summary>
    public static List<Character> Roster(IReadOnlyList<Character> party, IEnumerable<Character> saved)
    {
        Character? Live(Character s) => party.FirstOrDefault(m => string.Equals(m.Name.Trim(), s.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        var list = saved.Select(s => Live(s) ?? s).ToList();
        foreach (var m in party) if (!list.Contains(m)) list.Add(m);
        return list;
    }

    /// <summary>The Look page, as <c>sub09B8</c> pieces.</summary>
    public static List<CharStatFile.Piece> Paint(IReadOnlyList<Entry> here)
    {
        var p = new List<CharStatFile.Piece>
        {
            new(2, 1, "Name", true), new(2, 9, "Guild", true), new(2, 15, "Level", true), new(2, 19, "Hits", true),
        };
        for (int i = 0; i < here.Count; i++)
        {
            var e = here[i];
            int row = i + 3;
            p.Add(new(row, 1, e.Name, false));
            p.Add(new(row, 9, e.Guild, false));
            p.Add(new(row, 15, e.Level.ToString(CultureInfo.InvariantCulture), false));
            p.Add(new(row, 19, e.Hits.ToString(CultureInfo.InvariantCulture), false));
        }
        return p;
    }

    /// <summary>
    /// <c>lblRoom</c> (MAZEMOD.BAS:309-331). The position and the floor are the leader's, and each
    /// lost flag hides its own part - but the floor is only hidden with the position: lost on the
    /// level alone still prints "x,y,level". Lost in position, it is "(Lost on N) ", and "(Lost) "
    /// when the floor is lost too.
    /// </summary>
    public static string Readout(Character shown, Character leader, int x, int y, int level, Facing facing, int living, int dead)
    {
        if (shown.IsDead) return "(You are Dead)";
        string s = !leader.LostPosition ? $"{x},{y},{level} "
                 : !leader.LostLevel ? $"(Lost on {level}) "
                 : "(Lost) ";
        s += leader.LostFacing ? "?" : facing.ToString();
        return s + (dead > 0 ? $" [ {living}+{dead} ]" : $" [ {living} ]");
    }
}
