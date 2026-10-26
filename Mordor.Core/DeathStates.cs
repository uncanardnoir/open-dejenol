namespace Mordor.Core;

/// <summary>
/// <c>gv0814$</c> (MODULE1.BAS:449-457), the eight ways a character can be off their feet. The
/// original stores <c>DeathState</c> counting from one - zero means alive - and reads the name back
/// as <c>gv0814$(DeathState - 1)</c>, which is how the Dungeon window fills <c>lblDead</c>
/// (MAINMOD2.BAS:3043) and how the character list marks a body (FRMCHARL.BAS:53).
///
/// "Annihalated" is spelt that way in the source and is spelt that way here.
/// </summary>
public static class DeathStates
{
    public static readonly string[] Names =
        { "Dead", "Stoned", "Destroyed", "Immolated", "Frozen", "Annihalated", "Melted", "Rock" };

    /// <summary>
    /// What to call a body. The port keeps the original's <c>DeathState</c> numbering in
    /// <see cref="Character.CauseOfDeath"/> - <see cref="Rules.DeathStoned"/> is 2 and
    /// <see cref="Rules.DeathRock"/> is 8, which are the values that index this list - except that
    /// an ordinary death is 0 here where the original writes 1. Both mean "Dead".
    /// </summary>
    public static string Name(int causeOfDeath)
    {
        int i = Math.Max(1, causeOfDeath) - 1;
        return i >= 0 && i < Names.Length ? Names[i] : Names[0];
    }
}
