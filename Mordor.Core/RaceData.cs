namespace Mordor.Core;

public enum Sex { Male, Female }

public enum RaceSize { Small = 1, Normal = 2, Big = 3, VeryBig = 4 }

public enum Stat { Str, Int, Wis, Con, Cha, Dex }

/// <summary>
/// Character race definition, loaded from MDATA1 (see <see cref="RaceData.LoadFrom"/>); there is
/// no built-in table.
/// Stat order is STR, INT, WIS, CON, CHA, DEX.
/// </summary>
public sealed class Race
{
    public required string Name { get; init; }
    public required int[] Min { get; init; }   // length 6
    public required int[] Max { get; init; }
    public required RaceSize Size { get; init; }
    /// <summary>bit0 Good, bit1 Neutral, bit2 Evil</summary>
    public int AlignMask { get; init; } = 7;
    public int BonusPoints { get; init; }
    public int MaxAge { get; init; }
    public float ExpFactor { get; init; }

    /// <summary>Base resistance percentages, one per kind. MDATA1 stores twelve; the game uses eleven.</summary>
    public int[] Resistances { get; init; } = new int[Effects.ResistanceKinds];

    public Alignment[] Alignments =>
        Enum.GetValues<Alignment>().Where(a => (AlignMask & (1 << (int)a)) != 0).ToArray();

    public int MinTotal => Min.Sum();
    public int MaxTotal => Max.Sum();
}

public static class RaceData
{
    public static readonly string[] StatNames = { "STR", "INT", "WIS", "CON", "CHA", "DEX" };

    /// <summary>
    /// The names the game spells out in prose - <c>gv05EE$</c>, MODULE1.BAS:481-486 - as against
    /// the three-letter headings above. A drained stat is reported with one of these.
    /// </summary>
    public static readonly string[] StatNamesLong =
        { "Strength", "Intelligence", "Wisdom", "Constitution", "Charisma", "Dexterity" };

    /// <summary>Empty until <see cref="LoadFrom"/> has read MDATA1; there is no built-in table to fall back on.</summary>
    public static IReadOnlyList<Race> All { get; private set; } = Array.Empty<Race>();

    /// <summary>
    /// The race table from MDATA1, as <c>loadDataFile</c> reads it into Races
    /// (MODULE1.BAS:693-696). The original has no fallback table: a failed read leaves it with
    /// nothing (MODULE1.BAS:909-911). Here a missing or unreadable file throws, and a file with no
    /// races in it throws too, for the caller to show - as <see cref="GuildData.LoadFrom"/> does.
    /// </summary>
    public static void LoadFrom(string gameDir)
    {
        string path = Path.Combine(gameDir, "MDATA1.MDR");
        var races = MdrLoader.LoadRaces(path);
        if (races.Count == 0) throw new InvalidDataException($"No races could be read from {path}.");
        All = races;
    }

    /// <summary>
    /// The race of this name. The original reaches a race by its number, never by name
    /// (<c>Races(cmbRace.ListIndex)</c>, CHARCREA.BAS:63), so a name that matches no race is a port
    /// bug or a damaged save; it throws rather than quietly standing in the first race.
    /// </summary>
    public static Race ByName(string name) =>
        All.FirstOrDefault(r => r.Name == name)
        ?? throw new KeyNotFoundException($"No race named \"{name}\" in MDATA1 ({All.Count} races loaded).");

    /// <summary>Index of a race in file order (this is the bit position used by guild race masks).</summary>
    public static int IndexOf(string name)
    {
        for (int i = 0; i < All.Count; i++) if (All[i].Name == name) return i;
        return -1;
    }
}
