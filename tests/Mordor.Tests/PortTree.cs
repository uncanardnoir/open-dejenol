/// <summary>
/// The source tree these probes were built from, and which of its private parts it has.
///
/// The probes read the port's own source - the C#, the docs - from the tree they were built in
/// (the folder above the build holding Mordor.sln), never from beside the game folder, so a check
/// always reads the tree it is checking. The port's projects are under <c>src/</c>, the checks
/// under <c>tests/</c>, the tools under <c>tools/</c>.
///
/// Some checks also read the decompiled original (<c>decompiled/</c>, <c>decompiled-named/</c>) or
/// the saved dejenol.com pages (<c>docs/dejenol-formulas/</c>). The developer's own tree has them;
/// the public tree (tools/export-public.sh) does not. Without them those checks print
/// <see cref="SkipLine"/> and are not counted, rather than failing on text that is not there.
/// </summary>
public static class PortTree
{
    /// <summary>What a check that needs the decompiled source prints when it is not here.</summary>
    public const string SkipLine = "skipped: decompiled source not present";

    /// <summary>
    /// The root of the tree this build came from: the nearest folder above the build that holds
    /// Mordor.sln (the build is tests/Mordor.Tests/bin/&lt;config&gt;/net8.0, or Mordor.Tools' in
    /// tools/, so five folders up; the walk does not depend on the depth).
    /// </summary>
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Mordor.sln"))) return d.FullName;
        throw new DirectoryNotFoundException($"no Mordor.sln above {AppContext.BaseDirectory}: these checks read the source tree they were built in");
    }

    /// <summary>Both copies of the decompiled original are here.</summary>
    public static bool HasDecompiled =>
        Directory.Exists(Path.Combine(Root, "decompiled")) && Directory.Exists(Path.Combine(Root, "decompiled-named"));

    /// <summary>The saved dejenol.com pages are here.</summary>
    public static bool HasFormulas => Directory.Exists(Path.Combine(Root, "docs", "dejenol-formulas"));

    /// <summary>The first part of a path under the root names a private folder (decompiled/, decompiled-named/, docs/dejenol-formulas).</summary>
    public static bool IsPrivate(params string[] parts) =>
        parts.Length > 0 && (parts[0] is "decompiled" or "decompiled-named"
                             || (parts[0] == "docs" && parts.Length > 1 && parts[1] == "dejenol-formulas"));

    /// <summary>The private folder that path is in is absent from this tree.</summary>
    public static bool PrivateMissing(params string[] parts) =>
        IsPrivate(parts) && (parts[0] == "docs" ? !HasFormulas : !HasDecompiled);

    /// <summary>A path under the root.</summary>
    public static string At(params string[] parts) =>
        Path.GetFullPath(Path.Combine(new[] { Root }.Concat(parts).ToArray()));
}

/// <summary>
/// Thrown by a check reading the decompiled source in a tree without it; the section it is in is
/// reported as skipped (SelfCheckProbe.Section) and nothing after the read runs.
/// </summary>
public sealed class DecompiledMissing : Exception
{
    public DecompiledMissing(string path) : base(PortTree.SkipLine + ": " + path) { }
}
