using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mordor.Modding;

/// <summary>
/// One on/off setting a mod offers the player. The host draws it as a checkbox on the Options
/// window's Mods page and keeps it in settings.ini under the mod's own section.
/// </summary>
public sealed class ModToggleSpec
{
    /// <summary>The key in settings.ini. Letters, digits and underscores.</summary>
    public string Key { get; set; } = "";
    /// <summary>The checkbox caption.</summary>
    public string Label { get; set; } = "";
    /// <summary>A sentence or two under the caption.</summary>
    public string Description { get; set; } = "";
    /// <summary>What it is until the player changes it.</summary>
    public bool Default { get; set; }
}

/// <summary>
/// A settings window of its own a mod asks the Mods page for (API version 11), in place of its
/// settings listed on the page: a "Settings..." button opens it, from the main menu's page and the
/// in-game one, also while the mod is off. Its groups each have a heading and the settings named by
/// key - toggles from <see cref="ModManifest.Settings"/>, choices from <see cref="ModManifest.Choices"/>.
/// A group holding toggles has a box on its heading that switches them all (shown mixed when they
/// differ).
/// </summary>
public sealed class ModSettingsWindowSpec
{
    /// <summary>The window's title.</summary>
    public string Title { get; set; } = "";
    public List<ModSettingsGroupSpec> Groups { get; set; } = new();
}

/// <summary>One group of a mod's settings window (<see cref="ModSettingsWindowSpec"/>).</summary>
public sealed class ModSettingsGroupSpec
{
    /// <summary>The heading.</summary>
    public string Label { get; set; } = "";
    /// <summary>A short line about the group, the heading's tooltip.</summary>
    public string Description { get; set; } = "";
    /// <summary>The settings in it, by key, in order: toggles and choices.</summary>
    public List<string> Keys { get; set; } = new();
}

/// <summary>
/// A choice setting a mod offers the player: one of a few options (API version 10). The Mods page
/// draws it as radio buttons under its caption; settings.ini keeps the chosen option's text under
/// the key (<see cref="ModSettings.GetChoice"/>).
/// </summary>
public sealed class ModChoiceSpec
{
    /// <summary>The key in settings.ini. Letters, digits and underscores.</summary>
    public string Key { get; set; } = "";
    /// <summary>The caption above the options.</summary>
    public string Label { get; set; } = "";
    /// <summary>A short line about it.</summary>
    public string Description { get; set; } = "";
    /// <summary>The options, as shown and as kept.</summary>
    public List<string> Options { get; set; } = new();
    /// <summary>
    /// A short line for each option, in the options' order, shown as its radio button's tooltip
    /// (API version 12). An option without one (an empty line, or none given) has the choice's
    /// <see cref="Description"/> as its tooltip.
    /// </summary>
    public List<string> OptionDescriptions { get; set; } = new();

    /// <summary>The tooltip for an option: its own line, else the choice's description.</summary>
    public string TooltipFor(string option)
    {
        int i = Options.IndexOf(option);
        return i >= 0 && i < OptionDescriptions.Count && !string.IsNullOrWhiteSpace(OptionDescriptions[i]) ? OptionDescriptions[i] : Description;
    }
    /// <summary>The option it is until the player changes it (the first when empty or not one of them).</summary>
    public string Default { get; set; } = "";
}

/// <summary>
/// A list setting a mod offers the player: rows of text cells under fixed column headings (the
/// Kaizen mod's buff list: caster, spell or item, target). The host draws it on the Mods page as
/// an editor - free-text cells, rows added, removed and moved - and keeps it in settings.ini under
/// the mod's own section, as one line (<see cref="ModSettings.GetRows"/>).
/// </summary>
public sealed class ModTableSpec
{
    /// <summary>The key in settings.ini. Letters, digits and underscores.</summary>
    public string Key { get; set; } = "";
    /// <summary>The caption above the editor.</summary>
    public string Label { get; set; } = "";
    /// <summary>A sentence or two under the caption.</summary>
    public string Description { get; set; } = "";
    /// <summary>The column headings, one cell per column in every row.</summary>
    public List<string> Columns { get; set; } = new();
}

/// <summary>
/// <c>mod.json</c>, the file that makes a folder under <c>mods/</c> a mod.
///
/// <code>
/// {
///   "id": "QoL",
///   "name": "Quality of Life",
///   "version": "1.0.0",
///   "author": "...",
///   "gameVersion": "1.1",
///   "assembly": "Mordor.Mod.QoL.dll",
///   "entryType": "Mordor.Mod.QoL.QolMod",
///   "description": "...",
///   "settings": [ { "key": "ItemSort", "label": "Item sort", "description": "...", "default": true } ]
/// }
/// </code>
/// </summary>
public sealed class ModManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    /// <summary>The game version the mod was built against; see <see cref="ModApi.GameVersion"/>.</summary>
    public string GameVersion { get; set; } = "";
    /// <summary>The mod's .dll, relative to its folder.</summary>
    public string Assembly { get; set; } = "";
    /// <summary>The full name of the class that implements <see cref="IMod"/>.</summary>
    public string EntryType { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>
    /// Toggles declared up front, so the Mods page can show them while the mod is switched off
    /// (and so not loaded). A mod may also declare more from code, with
    /// <see cref="ModSettings.DeclareToggle"/>; those show only while it is loaded.
    /// </summary>
    public List<ModToggleSpec> Settings { get; set; } = new();
    /// <summary>
    /// List settings (<see cref="ModTableSpec"/>), shown on the Mods page under the toggles, also
    /// while the mod is switched off. Empty by default.
    /// </summary>
    public List<ModTableSpec> Tables { get; set; } = new();
    /// <summary>
    /// Choice settings (<see cref="ModChoiceSpec"/>), shown on the Mods page above the toggles, also
    /// while the mod is switched off. Empty by default. API version 10.
    /// </summary>
    public List<ModChoiceSpec> Choices { get; set; } = new();
    /// <summary>
    /// A settings window of the mod's own (<see cref="ModSettingsWindowSpec"/>, API version 11), or
    /// null: its settings are then listed on the Mods page itself.
    /// </summary>
    public ModSettingsWindowSpec? SettingsWindow { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ModManifest Read(string path) =>
        JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException(path + " is empty");

    /// <summary>What is wrong with it, or null when it can be loaded.</summary>
    [JsonIgnore]
    public string? Problem =>
        string.IsNullOrWhiteSpace(Id) ? "no id"
        : Id.IndexOfAny(new[] { '[', ']', '=', '\r', '\n' }) >= 0 ? "the id may not contain [ ] or ="
        : string.IsNullOrWhiteSpace(Assembly) ? "no assembly"
        : string.IsNullOrWhiteSpace(EntryType) ? "no entryType"
        : null;

    /// <summary>"Quality of Life 1.0.0", or the id when it has no name.</summary>
    [JsonIgnore]
    public string Title => (string.IsNullOrWhiteSpace(Name) ? Id : Name) + (Version == "" ? "" : " " + Version);
}

/// <summary>Constants a mod can check itself against.</summary>
public static class ModApi
{
    /// <summary>The original's own version, "Mordor 1.1" (App.Title in MORDOR.MAK).</summary>
    public const string GameVersion = "1.1";

    /// <summary>Raised whenever a hook is added or its meaning changes.</summary>
    public const int ApiVersion = 13;

    /// <summary>The file that marks a mod's folder.</summary>
    public const string ManifestFile = "mod.json";
}
