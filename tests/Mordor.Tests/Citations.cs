using System.Text.RegularExpressions;

/// <summary>
/// Every "this rule comes from FILE.BAS:LINE" in the document and in the code, checked against the
/// file it names.
///
/// A wrong citation is hard to spot by eye because the wrong line still lands on *something*.
/// sub0B82 was cited as MONSMOD.BAS:606-640 and lives in MAINMOD2 at 4704; MONSMOD:604 is where
/// sub0C00 begins, so the citation pointed two lines into a real routine and read as plausible for
/// months.
///
/// Two naming schemes are in use and they need different handling.
///
/// **The decompiler's own names** - <c>subXXXX</c>, <c>fnXXXX</c> - live in <c>decompiled/</c>
/// under module numbers, <c>MODULE4.BAS</c>. Citations spell the file either way, so MONSMOD has to
/// be resolved to MODULE4.
///
/// **The recovered names** - <c>mdrcharTeleport</c>, <c>monGetGold</c>, <c>cmdMake_Click</c> - live
/// in <c>decompiled-named/</c>, which uses the real file names. Its line numbers differ from the
/// raw copy by a few, and this project's citations use *both* conventions: mdrmulSwitch
/// (MULTIMOD.BAS:605) is a named line and medPlotMaze (FRMAUTOM.BAS:390) is a raw one. The two
/// copies list the same routines in the same order, so the Nth in one is the Nth in the other, and
/// a citation is accepted if it lands inside either.
///
/// A citation may also point at a line that *calls* the routine rather than defines it -
/// "sub085B's line when it works (MAINMOD2.BAS:3434)" - and that is correct usage, so a line
/// carrying the name is accepted wherever it is.
///
/// It cannot tell a citation of absence from one of presence. A comment saying "there is no
/// sub085B anywhere in (MONSMOD.BAS:119-149)" reads to this as a claim that there is one, and
/// the only cure is to word it so the routine name is not the last thing before the bracket.
/// </summary>
public static class Citations
{
    /// <summary>What the decompiler called each module, against what it really is.</summary>
    public static readonly Dictionary<string, string> RawNameOf = new()
    {
        ["MAINMOD"] = "MODULE2", ["MAZEMOD"] = "MODULE3", ["MONSMOD"] = "MODULE4",
        ["MAINMOD2"] = "MODULE6", ["MULTIMOD"] = "MODULE7", ["MUSIC"] = "MODULE9",
        ["HALL"] = "MODULE11", ["GRAPHICS"] = "MODULE12",
    };

    /// <summary>A routine and the lines it spans, one-based and inclusive of its End.</summary>
    public readonly record struct Routine(string Name, int Start, int End);

    /// <summary>Every Sub and Function a .BAS file declares, in the order it declares them.</summary>
    public static List<Routine> RoutinesIn(string path)
    {
        var found = new List<Routine>();
        var lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            var d = Regex.Match(lines[i], @"^(?:Private\s+)?(?:Sub|Function)\s+([A-Za-z][A-Za-z0-9_]*)");
            if (!d.Success) continue;
            int end = lines.Length;
            for (int j = i + 1; j < lines.Length; j++)
                if (lines[j].StartsWith("End Sub") || lines[j].StartsWith("End Function")) { end = j + 1; break; }
            found.Add(new Routine(d.Groups[1].Value, i + 1, end));
        }
        return found;
    }

    /// <summary>
    /// Anchored on the citation and scanning **backwards** for the nearest routine name. Matching a
    /// name and then looking forward for a bracket reads
    /// "lblName_Click -> mdrmulSwitch (MULTIMOD.BAS:605)" as a claim about lblName_Click, when the
    /// bracket plainly belongs to mdrmulSwitch. The nearest name before it is the one meant.
    /// </summary>
    private const string Citation = @"\(([A-Z][A-Z0-9]*)\.BAS:(\d+)(?:-(\d+))?";

    /// <summary>
    /// A routine name: the decompiler's own, or a recovered one. The recovered forms need a capital
    /// somewhere after the prefix, or the English word "monsters" reads as a routine.
    /// </summary>
    private const string Name =
        @"\b((?:sub|fn)[0-9A-F]{4}|(?:mdr|mon|med)[a-z0-9_]*[A-Z][A-Za-z0-9_]*"
      + @"|[A-Za-z]{3,}_(?:Click|Load|Unload|Resize|DblClick|KeyPress|KeyDown|DragDrop|Change|Timer))\b";

    /// <summary>One citation as found: who it names, which file, which line.</summary>
    public readonly record struct Cite(string Where, string Name, string File, int Line, int Last);

    /// <summary>Every citation in a file, each attributed to the nearest name before it.</summary>
    public static List<Cite> Find(string path)
    {
        string text = File.ReadAllText(path);
        string where = Path.GetFileName(path);
        var found = new List<Cite>();
        foreach (Match m in Regex.Matches(text, Citation))
        {
            int from = Math.Max(0, m.Index - 40);
            // back to a word boundary, or the window can start inside a word and turn
            // lblBuffer_DragDrop into blBuffer_DragDrop
            while (from > 0 && (char.IsLetterOrDigit(text[from - 1]) || text[from - 1] == '_')) from--;
            var names = Regex.Matches(text.Substring(from, m.Index - from), Name);
            if (names.Count == 0) continue;
            int first = int.Parse(m.Groups[2].Value);
            // a citation is often a range - "(MAINMOD2.BAS:516-519)" - and the routine or the call
            // may be anywhere in it, so the whole span has to be considered
            int last = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : first;
            found.Add(new Cite(where, names[^1].Groups[1].Value, m.Groups[1].Value, first, last));
        }
        return found;
    }

    /// <summary>
    /// Check every citation in these files. Returns one line per problem, and the number that
    /// resolved - a count of zero means the patterns have stopped matching, which is the way this
    /// check failed the first time it was written.
    /// </summary>
    public static (List<string> Problems, int Resolved) Check(IEnumerable<string> files, string portRoot)
    {
        string dec = Path.Combine(portRoot, "decompiled");
        string named = Path.Combine(portRoot, "decompiled-named");

        var rawCache = new Dictionary<string, List<Routine>>();
        var namedCache = new Dictionary<string, List<Routine>>();
        List<Routine>? Load(Dictionary<string, List<Routine>> cache, string dir, string file)
        {
            if (cache.TryGetValue(file, out var got)) return got;
            string p = Path.Combine(dir, file + ".BAS");
            var r = File.Exists(p) ? RoutinesIn(p) : null;
            if (r != null) cache[file] = r;
            return r;
        }

        // every routine name the named copy knows, so an invented one can be told from a typo
        var known = new HashSet<string>();
        if (Directory.Exists(named))
            foreach (string f in Directory.GetFiles(named, "*.BAS"))
                foreach (var r in RoutinesIn(f)) known.Add(r.Name);
        if (Directory.Exists(dec))
            foreach (string f in Directory.GetFiles(dec, "*.BAS"))
                foreach (var r in RoutinesIn(f)) known.Add(r.Name);

        var problems = new List<string>();
        int resolved = 0;

        foreach (string path in files)
            foreach (var c in Find(path))
            {
                if (!known.Contains(c.Name)) { problems.Add($"{c.Where}: {c.Name} is not a routine"); continue; }

                // the named copy first, under the file as written; then the raw copy, under the
                // module number, at the same position in the routine list
                var inNamed = Load(namedCache, named, c.File);
                int at = inNamed?.FindIndex(r => r.Name == c.Name) ?? -1;

                string rawFile = RawNameOf.TryGetValue(c.File, out string? rn) ? rn : c.File;
                var inRaw = Load(rawCache, dec, rawFile);
                int rawAt = inRaw?.FindIndex(r => r.Name == c.Name) ?? -1;

                // Does the cited line call it? A routine is very often cited where it is *used*
                // rather than where it lives - sub1536 is defined in MULTIMOD and cited at the
                // FRMGROUP line that calls it - so this has to be asked before concluding the
                // routine is in the wrong file, not after.
                bool callsIt = false;
                foreach (var (dir, file) in new[] { (named, c.File), (dec, RawNameOf.TryGetValue(c.File, out string? r2) ? r2 : c.File) })
                {
                    string p = Path.Combine(dir, file + ".BAS");
                    if (!File.Exists(p)) continue;
                    var ls = File.ReadAllLines(p);
                    for (int n = c.Line; n <= c.Last && !callsIt; n++)
                        if (n >= 1 && n <= ls.Length && ls[n - 1].Contains(c.Name)) callsIt = true;
                    if (callsIt) break;
                }
                if (callsIt) { resolved++; continue; }

                if (at < 0 && rawAt < 0)
                {
                    problems.Add($"{c.Where}: {c.Name} is named at {c.File}:{c.Line}-{c.Last}, which neither defines nor calls it");
                    continue;
                }
                resolved++;

                bool ok = false;
                if (at >= 0)
                {
                    ok = c.Line <= inNamed![at].End && c.Last >= inNamed[at].Start;
                    // the same routine's extent in the raw copy, matched by position
                    if (!ok && inRaw != null && at < inRaw.Count)
                        ok = c.Line <= inRaw[at].End && c.Last >= inRaw[at].Start;
                }
                if (!ok && rawAt >= 0)
                    ok = c.Line <= inRaw![rawAt].End && c.Last >= inRaw[rawAt].Start;

                if (!ok)
                {
                    var span = at >= 0 ? inNamed![at] : inRaw![rawAt];
                    problems.Add($"{c.Where}: {c.Name}@{c.File}:{c.Line} (it is {span.Start}-{span.End})");
                }
            }

        return (problems, resolved);
    }

    /// <summary>The document and every C# file the port is written in.</summary>
    public static List<string> FilesToCheck(string portRoot)
    {
        var files = new List<string> { Path.Combine(portRoot, "docs", "FIDELITY.md") };
        foreach (string proj in new[] { "src/Mordor.Core", "src/Mordor.App", "src/Mordor.Modding" })
        {
            string d = Path.Combine(portRoot, proj);
            if (!Directory.Exists(d)) continue;
            files.AddRange(Directory.GetFiles(d, "*.cs")
                .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)));
        }
        // the mods that come with the port cite the original too (Item sort, for one)
        string mods = Path.Combine(portRoot, "src/Mods");
        if (Directory.Exists(mods))
            files.AddRange(Directory.GetFiles(mods, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                         && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)));
        return files.Where(File.Exists).OrderBy(p => p).ToList();
    }

    /// <summary>
    /// Print every citation and what became of it, for working on one by hand: 1 when any is a
    /// problem (selfcheck asserts the same count), 0 when none is or the decompiled source is absent.
    /// </summary>
    public static int Run(string dir)
    {
        string root = PortTree.Root;
        if (!PortTree.HasDecompiled) { Console.WriteLine($"{PortTree.SkipLine}: the citations name routines in decompiled/ and decompiled-named/"); return 0; }
        var files = FilesToCheck(root);
        var (problems, resolved) = Check(files, root);
        Console.WriteLine($"{files.Count} files, {resolved} citations resolved, {problems.Count} problems\n");
        foreach (string p in problems) Console.WriteLine("  " + p);
        if (problems.Count == 0) Console.WriteLine("  every citation lands in the routine it names");
        return problems.Count == 0 ? 0 : 1;
    }
}
