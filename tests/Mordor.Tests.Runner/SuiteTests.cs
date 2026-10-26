using System.Diagnostics;
using System.Reflection;
using Xunit;

// One process at a time, as tools/check.sh runs them: the suites each make and remove scratch folders
// of their own, and the window checks lay real controls out, so nothing is gained by overlapping them.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Mordor.Tests.Runner;

/// <summary>
/// The existing suites under <c>dotnet test</c>: each test runs one suite as its own process, from
/// the folder its project built into, against the game folder named by <c>MORDOR_GAMEDIR</c> - the
/// same variable the app reads (docs/GAME_FILES.md). The suite's own output goes to the test's
/// output; the test fails on a non-zero exit or on any FAIL line, and the failure message lists those
/// lines under the heading of the section each came from. Without MORDOR_GAMEDIR it is skipped.
///
/// Nothing is rewritten here: the checks are the ones tools/check.sh runs, which stays the full gate
/// (it adds the tools' sweep and the app's start-up smoke).
/// </summary>
public sealed class SuiteTests(ITestOutputHelper output)
{
    [Fact(DisplayName = "selfcheck: the numbers that have a source to be checked against")]
    public void SelfCheck() => Run("Mordor.Tests", "selfcheck");

    [Fact(DisplayName = "mods: the loader, the default answers, the bundled mods")]
    public void Mods() => Run("Mordor.Tests", "mods");

    [Fact(DisplayName = "citations: every FILE.BAS:LINE against the routine it names")]
    public void Citations() => Run("Mordor.Tests", "citations");

    [Fact(DisplayName = "windows: the headless window checks (Mordor.UiCheck)")]
    public void Windows() => Run("Mordor.UiCheck", null);

    /// <summary>The longest a suite may take before it is stopped and failed.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(20);

    private void Run(string project, string? suite)
    {
        string? game = Environment.GetEnvironmentVariable("MORDOR_GAMEDIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(game),
            "MORDOR_GAMEDIR is not set: set it to your Mordor folder (the full game or the shareware) to run the suites, "
            + "e.g. MORDOR_GAMEDIR=/path/to/Mordor dotnet test");
        Assert.True(Directory.Exists(game), $"MORDOR_GAMEDIR names no folder: {game}");

        string dir = BuildDir(project);
        string exe = Path.Combine(dir, OperatingSystem.IsWindows() ? project + ".exe" : project);
        Assert.True(File.Exists(exe), $"{project} is not built at {exe}");

        var start = new ProcessStartInfo(exe)
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.GetFullPath(game!));
        if (suite != null) start.ArgumentList.Add(suite);
        start.Environment["MORDOR_MUTE"] = "1";
        // the suite's apphost finds the runtime through DOTNET_ROOT when the SDK is a private one
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT")))
            start.Environment["DOTNET_ROOT"] = DotnetRoot();

        var lines = new List<string>();
        using var p = new Process { StartInfo = start };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (lines) lines.Add(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (lines) lines.Add(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        bool ended = p.WaitForExit(Limit);
        if (!ended) { try { p.Kill(entireProcessTree: true); } catch { } }
        p.WaitForExit();

        string[] all;
        lock (lines) all = lines.ToArray();
        foreach (string line in all) output.WriteLine(line);

        Assert.True(ended, $"{Name(project, suite)} did not finish within {Limit.TotalMinutes} minutes");
        var failed = Failures(all);
        int ok = all.Count(l => l.StartsWith("  ok  ", StringComparison.Ordinal));
        if (p.ExitCode != 0 || failed.Count > 0)
            Assert.Fail($"{Name(project, suite)}: exit {p.ExitCode}, {failed.Count(f => f.StartsWith("  "))} FAIL line(s), {ok} ok\n"
                        + string.Join("\n", failed.Take(80))
                        + (failed.Count > 80 ? $"\n... and {failed.Count - 80} more lines" : "")
                        + (failed.Count == 0 ? "\nthe last lines:\n" + string.Join("\n", all.TakeLast(15)) : ""));
    }

    /// <summary>Every FAIL line (and anything that looks like an unhandled exception), each under its section's heading.</summary>
    private static List<string> Failures(string[] lines)
    {
        var found = new List<string>();
        string? heading = null, shown = null;
        foreach (string line in lines)
        {
            if (line.Length > 0 && !char.IsWhiteSpace(line[0])) heading = line;
            bool bad = line.TrimStart().StartsWith("FAIL", StringComparison.Ordinal)
                       || line.StartsWith("Unhandled exception", StringComparison.Ordinal);
            if (!bad) continue;
            if (heading != null && heading != shown && heading != line) { found.Add(heading); shown = heading; }
            found.Add(line.StartsWith("  ") ? line : "  " + line);
        }
        return found;
    }

    private static string Name(string project, string? suite) => suite == null ? project : $"{project} {suite}";

    /// <summary>The folder the project built into, in this build's configuration (set by the .csproj).</summary>
    private static string BuildDir(string project) =>
        typeof(SuiteTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == project + ".Dir")?.Value
        ?? throw new InvalidOperationException($"no {project}.Dir in the assembly's metadata");

    /// <summary>The .NET install this test runs on: shared/Microsoft.NETCore.App/&lt;version&gt;, three folders up.</summary>
    private static string DotnetRoot() =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "..", "..", ".."));
}
