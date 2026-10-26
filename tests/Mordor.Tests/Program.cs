// The test suites: the checks that assert, and whose exit code means something. tools/check.sh runs
// each of them and fails on a non-zero exit; the Mordor.Tests.Runner project runs them under
// `dotnet test`. The probes that only print are in Mordor.Tools (docs/TOOLS.md).
//
//   Mordor.Tests <gamedir> selfcheck    the numbers that have a source to check them against
//   Mordor.Tests <gamedir> mods         the mod system: loader, default answers, the bundled mods
//   Mordor.Tests <gamedir> citations    every FILE.BAS:LINE citation, each problem listed
//
// Each prints one "  ok  " or "  FAIL  " line per check (and "skip" lines for what this tree or
// this game folder cannot check) and exits 1 when anything failed.

var suites = new Dictionary<string, (string Description, Func<string, int> Run)>(StringComparer.Ordinal)
{
    ["selfcheck"] = ("the numbers that have a source to be checked against (dejenol.com, the help, the data, the decompiled source)",
                     SelfCheckProbe.Run),
    ["mods"] = ("the mod system: the loader, the default answers, the bundled QoL, Kaizen and DPS Meter mods (docs/MODDING.md)",
                ModProbe.Run),
    ["citations"] = ("every FILE.BAS:LINE citation in docs/FIDELITY.md and the code, against the routine it names",
                     Citations.Run),
};

if (args.Length == 0 || args[0] is "help" or "-h" or "--help" || (args.Length > 1 && args[1] is "help" or "-h" or "--help"))
{
    Console.WriteLine("usage: Mordor.Tests <gamedir> <suite>");
    Console.WriteLine();
    foreach (var (name, s) in suites) Console.WriteLine($"  {name,-10} {s.Description}");
    return args.Length == 0 ? 2 : 0;
}
if (args.Length < 2 || !suites.TryGetValue(args[1], out var suite))
{
    Console.WriteLine(args.Length < 2 ? "no suite named" : $"no suite called \"{args[1]}\"");
    Console.WriteLine("the suites: " + string.Join(", ", suites.Keys) + " (Mordor.Tests help lists them)");
    return 2;
}
return suite.Run(args[0]);
