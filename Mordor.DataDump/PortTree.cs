/// <summary>
/// The source tree these probes were built from, and which of its private parts it has.
///
/// The probes read the port's own source - the C#, the docs - from the tree they were built in
/// (four folders up from bin/&lt;config&gt;/net8.0), never from beside the game folder, so a check
/// always reads the tree it is checking.
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

    /// <summary>The root of the tree this build came from.</summary>
    public static string Root { get; } =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

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
