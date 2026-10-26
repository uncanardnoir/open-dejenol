namespace Mordor.Core;

/// <summary>
/// The four Character Options on the original's Control Panel (<c>FRMCONTR</c>, and the help's
/// screenshot at <c>game_files/bm0.png</c>). They live on the character as <c>M5914</c>, so they
/// are saved with them and follow them about; the Game Options window's settings are separate and
/// global, kept in the INI.
///
/// The indices are the original's own, which are not the order they appear on screen:
/// <c>M5914(0)</c> is Auto Change Lev, <c>M5914(2)</c> Hide, <c>M5914(3)</c> Preload Character,
/// <c>M5914(4)</c> No Perception Msgs. Index 1 is set true at creation and never read; index 5 is
/// the Combat Setting, which lives in <see cref="Character.CombatSetting"/>.
/// </summary>
public sealed class CharacterOptions
{
    /// <summary>
    /// "Turning this on will cause the Automap to automatically update the map to reflect what
    /// level the character is always on (if character is not lost)." Checked when a character is
    /// made - it is the only one of the four that starts on.
    /// </summary>
    public bool AutoChangeLevel { get; set; } = true;

    /// <summary>
    /// "Setting this option to True will make it so that no other character's can pick you up and
    /// carry you anywhere." A hidden body cannot be picked up, so it cannot be carried out of the
    /// dungeon by somebody else.
    /// </summary>
    public bool Hide { get; set; }

    /// <summary>
    /// "If you select this option, the character will be automatically loaded (without verification
    /// of the password) the next time Mordor is started." Note what that means: it bypasses the
    /// password, which is the whole of the protection on a character.
    /// </summary>
    public bool PreloadCharacter { get; set; }

    /// <summary>
    /// "Selecting this option will disable any perception messages (i.e. "You are pretty sure you
    /// are standing on an Anti-Magic square") while you are walking through the dungeon."
    ///
    /// <c>mdrmazPerceiveSquare</c> leaves before it does anything at all when this is set
    /// (MAZEMOD.BAS:459), so the roll is never made - the square is not quietly noticed and kept
    /// back, it is simply never looked at.
    /// </summary>
    public bool NoPerceptionMessages { get; set; }
}
