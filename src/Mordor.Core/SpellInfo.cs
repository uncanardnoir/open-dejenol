namespace Mordor.Core;

/// <summary>
/// The spell page of the character window's Misc tab - <c>mdrcharShowInfo</c> case <c>mc06BC</c>
/// (MAINMOD2.BAS:4813-4933), painted with <c>sub09B8</c> as <see cref="Paint"/> does. The Spells window's Info button asks for it by name, and the original
/// answers by painting it over there rather than where you clicked (FRMCHAR3.BAS:278-298).
///
/// "General Spell Information includes the Name and Class of the Spell. The Guild (based on the
/// most powerful guild you are a member of that teaches the spell) and spell points required to
/// cast the spell. Any special information (i.e. how many groups and monsters the spell affects if
/// it's a combat spell) and the Stats Required to cast the spell. You will also be told if the
/// spell is a 'mass' spell (i.e. affects the entire party)." - the help, Spell Information.
/// </summary>
public static class SpellInfo
{
    /// <summary>mc01D2: an effect of 200 or more is cast on the whole party, the real one 200 less.</summary>
    public const int MassOffset = 200;

    /// <summary>mc070A: effects 75 to 99 pick out one <see cref="MonsterKind"/>, 75 being kind 0.</summary>
    public const int OnlyAffectsBase = 75;

    /// <summary>mc0226: effects 50 to 60 grant the matching resistance.</summary>
    public const int ResistBase = 50;

    /// <summary>The Heal and Charm classes, which change what a damage figure is called.</summary>
    private const int HealClass = 9, CharmClass = 7;

    /// <summary>
    /// The "Special" line: what the spell actually does, in the original's own words. The order of
    /// the tests is the order of the Select Case, because VB stops at the first match.
    /// </summary>
    public static string Special(Spell s) =>
        SpecialFor(s, s.Effect >= MassOffset ? s.Effect - MassOffset : s.Effect) ?? "";

    /// <summary>
    /// The Select Case on the effect less any mass offset. "Resists" indexes <c>gv0620$</c> with the
    /// raw effect less 50, so a mass resistance would raise Subscript out of range; that is the null.
    /// </summary>
    private static string? SpecialFor(Spell s, int e)
    {

        if (e == 0 || (e >= OnlyAffectsBase && e <= 99))
        {
            if (s.Category == HealClass) return "Heals wounds";
            if (s.Category == CharmClass) return "Attempts to Bind/Charm Target(s)";
            return s.Effect == 100 ? "Kills target(s) or has no effect" : "Damages or Kills Target(s)";
        }
        switch (e)
        {
            case 1: return "Removes Poison";
            case 2: return "Removes Disease";
            case 3: return "Levitates a Character";
            case 4: return "Turns character Invisible";
            case 5: return "Allows character to see Invisible";
            case 6: return "Protects a Character";
            case 7: return "Teleports a Character";
            case 8: return "Displaces a Character";
            case 9: return "Opens a door in Wall";
            case 10: return "Removes Paralysis";
            case 11: return "Detects Rock";
            case 12: return "Detects Depth";
            case 13: return "Determines Location";
            case 14: return "Locates a Character";
            case 15: return "Pulls Character from Rock";
            case 16: return "Sets Sanctuary Coords";
            case 17: return "Teleports to Sanctuary Coords";
            case 18: return "Removes Poison and Disease";
            case >= 20 and <= 25: return "Raises the Dead";
            case 30: return "Unlocks Chest";
            case 31: return "Unlocks Door";
            case 32: return "Unlocks any locked object";
            case >= ResistBase and < ResistBase + 11:
            {
                int which = s.Effect - ResistBase;
                return which >= 0 && which < Librarian.Resistances.Length ? "Resists " + Librarian.Resistances[which] : null;
            }
            case 100: return "Kills target(s) or has no effect";
            case >= 40 and <= 42: return "Increases the Bind Level of a Companion";
            case 150: return "Binds a companion to the caster for life";
            default: return "Unknown!";
        }
    }

    /// <summary>
    /// The page as the original lays it out: Spell, Class, Damage or Heal, Guild, Points, and then
    /// Resistance if the spell has one, with the stats required beside them and the Special line
    /// beneath.
    /// </summary>
    public static List<CharStatFile.Piece> Paint(Spell s, Character c)
    {
        var p = new List<CharStatFile.Piece>();
        void Put(int row, int col, string text, bool bold = false, bool italic = false) =>
            p.Add(new CharStatFile.Piece(row, col, text, bold, italic));
        string N(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);   // CStr

        Put(2, 1, "Spell:", bold: true);
        Put(2, 9, "\"" + s.Name.TrimEnd() + "\"");
        Put(3, 1, "Class:", bold: true);
        // gv07B0$ is ReDim'd 0 To 18 with eighteen names, so slot 18 is blank and past it the page stops
        if (s.Category < 0 || s.Category > Librarian.SpellSchools.Length) return p;
        Put(3, 9, s.Category == Librarian.SpellSchools.Length ? "" : Librarian.SpellSchools[s.Category]);
        // "15/5 ... <base>/<levelmodifier>" (help): M5B33 and M5B41 + 1
        if (s.Damage2 > 0)
        {
            Put(4, 1, s.Category == HealClass ? "Heal:" : "Damage:", bold: true);
            Put(4, 9, N(s.Damage2) + "/" + N(s.SpecialEffect + 1));
        }
        Put(5, 1, "Guild:", bold: true);
        Put(5, 9, GuildData.All.Count > 0 ? GuildData.All[Spellbook.TeachingGuild(c, s)].Name : "");
        Put(6, 1, "Points:", bold: true);
        Put(6, 9, N(Spellbook.Cost(s, c)));
        if (s.ResistIndex > 0)
        {
            Put(8, 1, "Resistance:", bold: true);   // eleven letters from column 1, so it runs into column 9
            if (s.ResistIndex >= Librarian.Resistances.Length) return p;   // Subscript out of range: the page stops
            Put(8, 9, Librarian.Resistances[s.ResistIndex]);
        }
        Put(4, 14, "Stats Required", bold: true);
        int n = 0;
        for (int i = 0; i < 6 && i < s.Required.Length; i++)
            if (s.Required[i] > 0) Put(5 + n++, 16, RaceData.StatNamesLong[i][..3] + " (" + N(s.Required[i]) + ")");

        int e = s.Effect;
        if (e >= MassOffset)
        {
            Put(12, 1, "This spell affects the entire party", italic: true);
            e -= MassOffset;
        }
        string? special = SpecialFor(s, e);
        if (special == null) return p;   // "Resists" indexed past the list: the error ends the page here
        Put(10, 1, "Special", bold: true);
        Put(11, 1, special);
        // "Only Affects Xs" with the raw effect, so a mass one would index past the classes
        if (e >= OnlyAffectsBase && e <= 99)
        {
            // MonsterClasses is ReDim'd 0 To NumMonsterClasses (MODULE1.BAS:709): one blank record on the end
            int cls = s.Effect - OnlyAffectsBase, classes = Mdata1.MonsterClasses.Count;
            if (cls > classes) return p;
            Put(12, 1, "Only Affects " + (cls == classes ? "" : Mdata1.MonsterClassName(cls)) + "s");
        }
        if (s.Damage2 > 0 && s.Category != HealClass)
        {
            Put(14, 1, "This spell will affect " + N(s.Damage1) + " groups,");
            Put(15, 1, N(s.AffectGroup) + " monsters max in each group.");
        }
        Put(17, 1, "This spell is currently cast at a spell");
        int level = Spellbook.CasterSpellLevel(c, s);
        string trained = Spellbook.TrainingGuild < GuildData.All.Count ? GuildData.All[Spellbook.TrainingGuild].Name : "";
        Put(18, 1, "level of " + N(level) + " by your " + trained + " training.");
        return p;
    }

}
