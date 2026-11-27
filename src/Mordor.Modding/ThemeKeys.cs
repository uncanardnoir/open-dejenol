namespace Mordor.Modding;

/// <summary>
/// The game's colour roles, by the names it publishes them under in the application's resources
/// (docs/MODDING.md, "Colours"). Each is one brush the game recolours when the theme changes - Light,
/// the original's Windows 3.1 palette, or Dark - so a mod that takes its colours from these follows
/// the theme with nothing more to do: <c>Application.Current.FindResource(ThemeKeys.Text)</c>, or a
/// DynamicResource of the same name. A colour of a mod's own is drawn as it is, in either theme.
/// </summary>
public static class ThemeKeys
{
    // ---- grounds ----
    /// <summary>A window's own ground: the form's grey.</summary>
    public const string Window = "Mordor.Ground.Window";
    /// <summary>The ground inside a window's frame where its content sits: the Messages list, the Automap's map.</summary>
    public const string Well = "Mordor.Ground.Well";
    /// <summary>A list's or an input's ground: the party's rows, the spell list.</summary>
    public const string List = "Mordor.Ground.List";
    /// <summary>A field that only shows something (a label with a border).</summary>
    public const string Field = "Mordor.Ground.Field";
    /// <summary>A button's face.</summary>
    public const string Button = "Mordor.Ground.Button";
    /// <summary>A heading band (the Help's).</summary>
    public const string Banner = "Mordor.Ground.Banner";

    // ---- lines ----
    /// <summary>The light edge of a bevel.</summary>
    public const string Highlight = "Mordor.Line.Highlight";
    /// <summary>The dark edge of a bevel, and a plain frame's line.</summary>
    public const string Shadow = "Mordor.Line.Shadow";
    /// <summary>A button's black line, round it and between buttons that touch.</summary>
    public const string Outline = "Mordor.Line.Outline";

    // ---- inks ----
    /// <summary>Ordinary text.</summary>
    public const string Text = "Mordor.Ink.Text";
    /// <summary>Text that says less: a hint, a thing out of reach.</summary>
    public const string Muted = "Mordor.Ink.Muted";
    /// <summary>A table's column captions (the Party window's).</summary>
    public const string Caption = "Mordor.Ink.Caption";
    /// <summary>The form's navy: headings and labels.</summary>
    public const string Navy = "Mordor.Ink.Navy";
    /// <summary>The form's blue.</summary>
    public const string Blue = "Mordor.Ink.Blue";
    /// <summary>The form's maroon; and what goes badly - a foe's blow.</summary>
    public const string Maroon = "Mordor.Ink.Maroon";
    /// <summary>The form's green; and what goes well - the party's doing.</summary>
    public const string Green = "Mordor.Ink.Green";
    /// <summary>A warning, and a chest's news.</summary>
    public const string Warning = "Mordor.Ink.Warning";
    /// <summary>A name picked out: a monster's, a companion's, a notice.</summary>
    public const string Name = "Mordor.Ink.Name";
    /// <summary>Something out of the ordinary: a join, a spell's.</summary>
    public const string Magic = "Mordor.Ink.Magic";

    // ---- selection ----
    /// <summary>A chosen row's or selected text's ground.</summary>
    public const string Selection = "Mordor.Selection.Ground";
    /// <summary>A chosen row's or selected text's ink.</summary>
    public const string SelectionText = "Mordor.Selection.Text";

    // ---- bars (API version 18) ----
    // The Quality of Life mod's experience bar and the text over it. In Dark the fills are see-through
    // (alpha 0x80, over the track or the green); in Light every one is solid, its inks dark.
    /// <summary>The experience bar's fill short of the next level.</summary>
    public const string XpToNext = "Mordor.Bar.ToNext";
    /// <summary>The experience bar's fill past the level, toward the pin, drawn over a bar full of <see cref="XpToNext"/>.</summary>
    public const string XpToPin = "Mordor.Bar.ToPin";
    /// <summary>The experience bar pinned: the whole bar, over <see cref="XpToNext"/>.</summary>
    public const string XpPinned = "Mordor.Bar.Pinned";
    /// <summary>The experience bar's empty track.</summary>
    public const string XpTrack = "Mordor.Bar.Track";
    /// <summary>A figure written over a bar.</summary>
    public const string BarInk = "Mordor.Bar.Ink";
    /// <summary>A figure written over a bar past the level: what is still needed to the pin, and Pinned.</summary>
    public const string BarInkPast = "Mordor.Bar.InkPast";
    /// <summary>A guild quest's mark.</summary>
    public const string QuestMark = "Mordor.Ink.Quest";
    /// <summary>A guild quest's mark once the quest is done.</summary>
    public const string QuestDone = "Mordor.Ink.QuestDone";
}
