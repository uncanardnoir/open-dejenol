using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mordor.Core;

/// <summary>
/// A character's four companion slots, <c>Companions(4) As T57BF</c> (MAIN.TXT:339) walked
/// <c>0 To mc00E0 - 1</c>. A slot is empty when its monster index reads -1; here that is a null.
///
/// The slots are fixed. Nothing in the original closes a gap: a companion that dies stays where it
/// was, one that leaves or is sold leaves a hole, and a new one fills the first hole
/// (<c>fn1163</c>, MAINMOD2.BAS:383). So "Companion #3" is always the third slot, the panel rows
/// are numbered by slot, and the carried companion (<c>M5943</c>) is a slot number.
///
/// **An empty slot is still a record.** Only the monster index says it is empty; the name and
/// hits the last occupant left stay in it (<see cref="Left"/>). The name is blanked only by
/// <c>mdrcharShowCompanions</c>, and only for the character on screen (MAINMOD2.BAS:4745), and
/// <c>fn1163</c> fills a slot without writing a name (MAINMOD2.BAS:388-400). So a slot emptied
/// out of sight can hand its old name to the next creature charmed into it, or to a carry left
/// pointing at the hole.
///
/// Saves written while this port kept a compact list read back into slots 0 upwards, which is
/// where those companions were, and are padded to four. What an empty slot was left holding is
/// saved with it, as the original's character record keeps all four records whatever their
/// monster index says (<c>Companions(4) As T57BF</c>, MAIN.TXT:339): written as the record marked
/// <see cref="Companion.Vacant"/>, or as null when it is still the creator's blank, a name of one
/// space (CHARCREA.BAS:257) - which is also what a save from before 2026-10-19 reads back as.
/// </summary>
[JsonConverter(typeof(CompanionSlotsJson))]
public sealed class CompanionSlots
{
    /// <summary>mc00E0: four slots.</summary>
    public const int Slots = 4;

    private readonly Companion?[] _slots = new Companion?[Slots];

    /// <summary>What each empty slot's record still holds: the last occupant, or a blank.</summary>
    private readonly Companion[] _left = Enumerable.Range(0, Slots).Select(_ => Blank()).ToArray();

    /// <summary>A record as the character creator leaves it: Name " " (CHARCREA.BAS:257).</summary>
    private static Companion Blank() => new() { Name = " " };

    /// <summary>The companion in a slot, or null for an empty one (and for any slot out of range).
    /// Setting null lets the slot go as <see cref="Release"/> does.</summary>
    public Companion? this[int slot]
    {
        get => slot >= 0 && slot < Slots ? _slots[slot] : null;
        set
        {
            if (slot < 0 || slot >= Slots) return;
            if (value == null) Release(slot); else _slots[slot] = value;
        }
    }

    /// <summary>
    /// The record in a slot whatever its monster index says: the companion, or for an empty slot
    /// whatever the last one left there. The name fn128B reads, and what a stray spit lands on.
    /// </summary>
    public Companion Left(int slot) => _slots[slot] ?? _left[slot];

    /// <summary>
    /// What an empty slot's record holds, for the save: null when the slot is occupied, or when its
    /// record is still the creator's blank and there is nothing to keep.
    /// </summary>
    public Companion? Remains(int slot)
    {
        if (slot < 0 || slot >= Slots || _slots[slot] != null) return null;
        var r = _left[slot];
        bool blank = r.Name == " " && r.Hits == 0 && r.MaxHits == 0 && r.MonsterId == 0 && r.Ref == -1
                     && r.IdLevel == 0 && r.BindingLevel == 0 && r.Alignment == default;
        return blank ? null : r;
    }

    /// <summary>An empty slot's record put back from a save; an occupied slot is left alone.</summary>
    public void SetRemains(int slot, Companion record)
    {
        if (slot < 0 || slot >= Slots || _slots[slot] != null) return;
        record.Vacant = false;
        _left[slot] = record;
    }

    /// <summary>
    /// <c>Companions(n).Name = " "</c> for every empty slot, the one thing
    /// <c>mdrcharShowCompanions</c> writes (MAINMOD2.BAS:4745). The caller decides whether the
    /// character is the one on screen - the routine does nothing for anybody else (4716).
    /// </summary>
    public void BlankEmptyNames()
    {
        for (int i = 0; i < Slots; i++) if (_slots[i] == null) _left[i].Name = " ";
    }

    /// <summary>The slots that hold a companion, alive or dead, in slot order.</summary>
    public IEnumerable<(int Slot, Companion Companion)> Occupied
    {
        get
        {
            for (int i = 0; i < Slots; i++)
                if (_slots[i] is Companion c) yield return (i, c);
        }
    }

    /// <summary>Just the companions, in slot order.</summary>
    public IEnumerable<Companion> All => Occupied.Select(o => o.Companion);

    /// <summary>How many slots hold somebody.</summary>
    public int OccupiedCount => Slots - FreeSlots;

    /// <summary>Anybody at all.</summary>
    public bool Any => FreeSlots < Slots;

    /// <summary>
    /// <c>fn117A</c> (MAINMOD2.BAS:407): the empty slots. "No room for more Companions" is this
    /// coming back zero; a dead companion still takes its slot.
    /// </summary>
    public int FreeSlots
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Slots; i++) if (_slots[i] == null) n++;
            return n;
        }
    }

    /// <summary>The first hole, which is where <c>fn1163</c> puts a new companion (MAINMOD2.BAS:386-387); -1 when full.</summary>
    public int FirstFree
    {
        get
        {
            for (int i = 0; i < Slots; i++) if (_slots[i] == null) return i;
            return -1;
        }
    }

    /// <summary>
    /// Put a companion in the first hole. Returns the slot, or -1 with no room (and nothing done).
    /// <paramref name="keepSlotName"/> is fn1163's way (MAINMOD2.BAS:388-400), which sets
    /// everything but the name, so the newcomer answers to whatever the slot was last called;
    /// the Confinement's Buy blanks it first (FRMCITYC.BAS:127).
    /// </summary>
    public int Add(Companion c, bool keepSlotName = false)
    {
        int at = FirstFree;
        if (at < 0) return -1;
        if (keepSlotName) c.Name = _left[at].Name;
        _slots[at] = c;
        return at;
    }

    /// <summary><c>Companions(slot).Ref = -1</c>: the slot empties, and nothing moves up into it.</summary>
    public void Release(int slot)
    {
        if (slot < 0 || slot >= Slots || _slots[slot] is not Companion was) return;
        _left[slot] = was;          // the record stays, name and hits
        _slots[slot] = null;
    }

    /// <summary>Every slot emptied.</summary>
    public void ReleaseAll()
    {
        for (int i = 0; i < Slots; i++) Release(i);
    }

    /// <summary>"No use re-arranging a dead companion!" - FRMCHAR3.BAS:768, 772.</summary>
    public const string NoRearrangingTheDead = "No use re-arranging a dead companion!";

    /// <summary>
    /// One companion picture dropped on another, <c>picCompanion_DragDrop</c> (FRMCHAR3.BAS:763-779):
    /// "You can move and reorganize your companions by dragging and dropping the companion picture
    /// onto another companion slot" (help, Companions). Onto itself nothing happens; either end
    /// dead, the line above and nothing moves; otherwise the two records change places whole
    /// (<c>l02B0 = Companions(Index)</c> ... ). The carried companion, <c>M5943</c>, is a slot
    /// number the drop does not touch - and a dead one never moves, so it still points at its body.
    /// Returns the refusal, or null.
    /// </summary>
    /// <remarks>
    /// The original asks <c>Hits = 0</c>; a companion's hits are floored at nought
    /// (<c>mdrcharGetCarry</c>, MAINMOD2.BAS:2034-2035), so that is <see cref="Companion.IsDead"/>.
    /// Only a slot with a companion in it has a picture to drop on or drag (MAINMOD2.BAS:4740-4748).
    /// </remarks>
    public string? Rearrange(int onto, int from)
    {
        if (onto == from || this[onto] is not Companion there || this[from] is not Companion moving) return null;
        if (there.IsDead || moving.IsDead) return NoRearrangingTheDead;
        _slots[onto] = moving;
        _slots[from] = there;
        return null;
    }

    /// <summary>
    /// Each companion's table index written in, and one saved Unaligned given its creature's own
    /// alignment: <see cref="CompanionRef.Settle"/>. Answers how many records changed.
    /// </summary>
    public int Settle(IReadOnlyList<Monster> monsters) => CompanionRef.Settle(this, monsters);

    /// <summary>Which slot holds this companion, or -1.</summary>
    public int SlotOf(Companion c)
    {
        for (int i = 0; i < Slots; i++) if (ReferenceEquals(_slots[i], c)) return i;
        return -1;
    }
}

/// <summary>
/// Written as a four-entry array with nulls for the holes. Read from any array: entries go into
/// slots 0 upwards in order, a null stays a hole, and anything past the fourth is ignored - which
/// is how a save from the compact-list days comes back.
/// </summary>
public sealed class CompanionSlotsJson : JsonConverter<CompanionSlots>
{
    public override CompanionSlots Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var slots = new CompanionSlots();
        if (reader.TokenType == JsonTokenType.Null) return slots;
        var list = JsonSerializer.Deserialize<List<Companion?>>(ref reader, options) ?? new List<Companion?>();
        for (int i = 0; i < list.Count && i < CompanionSlots.Slots; i++)
        {
            // an empty slot's leftover record (MAINMOD2.BAS:383-400): the slot stays empty
            if (list[i] is Companion c && c.Vacant) slots.SetRemains(i, c);
            else slots[i] = list[i];
        }
        return slots;
    }

    public override void Write(Utf8JsonWriter writer, CompanionSlots value, JsonSerializerOptions options)
    {
        var list = new List<Companion?>();
        for (int i = 0; i < CompanionSlots.Slots; i++)
        {
            if (value[i] is Companion c) { list.Add(c); continue; }
            var r = value.Remains(i);
            list.Add(r == null ? null : new Companion
            {
                Ref = r.Ref, MonsterId = r.MonsterId, Name = r.Name, Hits = r.Hits, MaxHits = r.MaxHits,
                Alignment = r.Alignment, IdLevel = r.IdLevel, BindingLevel = r.BindingLevel, Vacant = true,
            });
        }
        JsonSerializer.Serialize(writer, list, options);
    }
}
