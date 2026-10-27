namespace Mordor.Core;

/// <summary>
/// The port's own update check (docs/RELEASING.md), as the launch settings
/// (<see cref="GameFolder.LaunchIniPath"/>, <c>%APPDATA%\OpenDejenol\settings.ini</c>) set it:
/// <code>
/// [Updates]
/// Check=False         ; no check at all (missing: check)
/// Prereleases=True    ; offer test releases (-alpha, -beta...) too; False: only stable ones
/// </code>
/// With no <c>Prereleases</c> line, a copy that is itself a test release is offered test releases
/// and a stable copy only stable ones, so a player who installed a beta goes on getting betas and
/// nobody else is offered one. Only a copy installed by the OpenDejenol installer checks at all
/// (Mordor.App's Updates); one run from a build folder never does.
/// </summary>
public static class UpdateSettings
{
    public const string Section = "Updates";
    public const string CheckKey = "Check";
    public const string PrereleasesKey = "Prereleases";

    /// <summary>Whether to look for an update at start: anything but a False (or 0, No, Off) there means yes.</summary>
    public static bool CheckEnabled(string? iniPath = null) =>
        CheckEnabled(Ini.Read(iniPath ?? GameFolder.LaunchIniPath));

    public static bool CheckEnabled(Ini ini) => !IsNo(ini.Text(Section, CheckKey));

    /// <summary>Whether test releases are offered to a copy of version <paramref name="currentVersion"/>.</summary>
    public static bool Prereleases(string? currentVersion, string? iniPath = null) =>
        Prereleases(currentVersion, Ini.Read(iniPath ?? GameFolder.LaunchIniPath));

    public static bool Prereleases(string? currentVersion, Ini ini)
    {
        string said = ini.Text(Section, PrereleasesKey);
        if (IsYes(said)) return true;
        if (IsNo(said)) return false;
        return IsPrerelease(currentVersion);
    }

    /// <summary>A SemVer test release: "0.2.0-beta.1" (anything after a '-' before any "+build").</summary>
    public static bool IsPrerelease(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        string v = version.Trim();
        int plus = v.IndexOf('+');
        if (plus >= 0) v = v[..plus];
        return v.Contains('-');
    }

    private static bool IsYes(string s) => s.Trim().ToLowerInvariant() is "true" or "1" or "yes" or "on";
    private static bool IsNo(string s) => s.Trim().ToLowerInvariant() is "false" or "0" or "no" or "off";
}
