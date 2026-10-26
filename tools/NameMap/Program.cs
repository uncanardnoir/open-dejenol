using System.Text;
using System.Text.RegularExpressions;

// Recovers the original identifiers from the decompiled Mordor source and records them in
// names.tsv, then renders an annotated copy of the source with those names applied.
//
// Every name carries a confidence column:
//   definitive - traced to a literal string in the program itself (the developer left
//                "Error '...' occurred in MODULE:procedure." handlers in most routines),
//                or a name VBDis never lost in the first place.
//   guess      - inferred by me from how the thing is used. Never trust without checking.
//
// usage: NameMap scan  <decompiledDir> <names.tsv>
//        NameMap apply <decompiledDir> <names.tsv> <outDir>

if (args.Length < 3) { Console.Error.WriteLine("usage: NameMap scan|apply <src> <names.tsv> [outDir]"); return 1; }
string mode = args[0], src = args[1], tsv = args[2];

var procRe = new Regex(@"^(?:Sub|Function)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
var endRe = new Regex(@"^End\s+(?:Sub|Function)", RegexOptions.Compiled);
var errRe = new Regex(@"occurred in ([A-Za-z0-9_]+):([A-Za-z0-9_]+)", RegexOptions.Compiled);
var mangledRe = new Regex(@"^(?:sub|fn|extsub|extfn)[0-9A-F]{4}$", RegexOptions.Compiled);

if (mode == "scan")
{
    // token -> (name, set of files it was seen in)
    var procNames = new Dictionary<string, string>(StringComparer.Ordinal);
    var conflicts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    var moduleVotes = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
    var confirmed = new List<(string file, string name)>();   // already-named procs the error string agrees with

    foreach (string path in Files(src))
    {
        string file = Path.GetFileNameWithoutExtension(path);
        string? cur = null;
        foreach (string line in File.ReadLines(path))
        {
            var pm = procRe.Match(line);
            if (pm.Success) { cur = pm.Groups[1].Value; continue; }
            if (endRe.IsMatch(line)) { cur = null; continue; }

            var em = errRe.Match(line);
            if (!em.Success || cur == null) continue;
            string mod = em.Groups[1].Value, proc = em.Groups[2].Value;

            if (!moduleVotes.TryGetValue(file, out var votes)) moduleVotes[file] = votes = new(StringComparer.OrdinalIgnoreCase);
            votes[mod] = votes.GetValueOrDefault(mod) + 1;

            if (!mangledRe.IsMatch(cur))
            {
                // the procedure already had its name; the error string is a check on the method
                if (string.Equals(cur, proc, StringComparison.Ordinal)) confirmed.Add((file, cur));
                continue;
            }
            if (procNames.TryGetValue(cur, out string? had) && had != proc)
            {
                if (!conflicts.TryGetValue(cur, out var set)) conflicts[cur] = set = new(StringComparer.Ordinal);
                set.Add(had); set.Add(proc);
            }
            else procNames[cur] = proc;
        }
    }

    foreach (string bad in conflicts.Keys) procNames.Remove(bad);

    var rows = new List<string[]>();

    // A module name has to end up on exactly one file. Routines get copied between modules and
    // bring their neighbour's error strings with them, so claims are resolved strongest-first and
    // a name already spoken for (or already the name of a real file) is not handed out twice.
    var taken = new HashSet<string>(Files(src).Select(Path.GetFileNameWithoutExtension)!, StringComparer.OrdinalIgnoreCase);
    var assigned = new Dictionary<string, (string Name, int Votes, int Total)>(StringComparer.OrdinalIgnoreCase);
    var claims = moduleVotes
        .SelectMany(kv => kv.Value.Select(v => (File: kv.Key, Name: v.Key, Votes: v.Value, Total: kv.Value.Values.Sum())))
        .OrderByDescending(c => c.Votes).ThenBy(c => c.File, StringComparer.OrdinalIgnoreCase);
    foreach (var c in claims)
    {
        if (string.Equals(c.Name, c.File, StringComparison.OrdinalIgnoreCase)) { assigned[c.File] = (c.File, c.Votes, c.Total); taken.Add(c.File); continue; }
        if (assigned.ContainsKey(c.File) || taken.Contains(c.Name)) continue;
        // a tie inside the file settles nothing; a single mention is fine once every stronger
        // claim on that name has been resolved elsewhere
        int top = moduleVotes[c.File].Values.Max();
        bool tied = moduleVotes[c.File].Values.Count(v => v == top) > 1;
        if (tied && c.Votes == top) continue;
        assigned[c.File] = (c.Name, c.Votes, c.Total);
        taken.Add(c.Name);
    }
    foreach (var (file, votes) in moduleVotes.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
    {
        if (!assigned.TryGetValue(file, out var a) || string.Equals(a.Name, file, StringComparison.OrdinalIgnoreCase)) continue;
        string note = votes.Count > 1
            ? $"error-string {a.Votes}/{a.Total} ({string.Join(" ", votes.OrderByDescending(v => v.Value).Select(v => v.Key + "=" + v.Value))})"
            : $"error-string x{a.Votes}";
        rows.Add(new[] { "module", file, a.Name, "definitive", note });
    }
    foreach (var (tok, name) in procNames.OrderBy(k => k.Key, StringComparer.Ordinal))
        rows.Add(new[] { "proc", tok, name, "definitive", "error-string in body" });

    using var w = new StreamWriter(tsv, false, new UTF8Encoding(false));
    w.WriteLine("# Original identifiers for the decompiled Mordor source.");
    w.WriteLine("# confidence: definitive = traced to a literal string in the program; guess = inferred, verify before trusting.");
    w.WriteLine("kind\told\tnew\tconfidence\tevidence");
    foreach (var r in rows) w.WriteLine(string.Join("\t", r));

    Console.WriteLine($"modules renamed : {rows.Count(r => r[0] == "module")}");
    Console.WriteLine($"procs recovered : {rows.Count(r => r[0] == "proc")}");
    Console.WriteLine($"cross-checks ok : {confirmed.Count} already-named procedures whose error string matches exactly");
    if (conflicts.Count > 0)
        Console.WriteLine($"CONFLICTS (left mangled): {string.Join(", ", conflicts.Select(c => c.Key + "->{" + string.Join("|", c.Value) + "}"))}");
    return 0;
}

if (mode == "apply")
{
    if (args.Length < 4) { Console.Error.WriteLine("apply needs an output directory"); return 1; }
    string outDir = args[3];
    Directory.CreateDirectory(outDir);

    var modules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var tokens = new Dictionary<string, (string New, string Conf)>(StringComparer.Ordinal);
    foreach (string line in File.ReadLines(tsv))
    {
        if (line.StartsWith('#') || line.StartsWith("kind\t")) continue;
        var f = line.Split('\t');
        if (f.Length < 5) continue;
        if (f[0] == "module") modules[f[1]] = f[2]; else tokens[f[1]] = (f[2], f[3]);
    }

    var tokenRe = tokens.Count == 0 ? null
        : new Regex(@"\b(" + string.Join("|", tokens.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)) + @")\b", RegexOptions.Compiled);

    // A member token is one source identifier, but the same identifier gets reused across
    // unrelated records - M5557 is an alignment bitmask in the item record and a four-entry
    // per-alignment count array in the shelf record. Naming such a member after what it does in
    // the first record you happened to read makes it a lie everywhere else, so say so out loud.
    var memberTypes = MemberTypes(src);
    foreach (var (tok, (name, _)) in tokens.OrderBy(t => t.Key, StringComparer.Ordinal))
        if (memberTypes.TryGetValue(tok, out var inTypes) && inTypes.Count > 1)
            Console.WriteLine($"  note: {tok} -> {name} is used by {inTypes.Count} record types ({string.Join(" ", inTypes)}); the name must fit all of them");

    int files = 0, hits = 0;
    foreach (string path in Files(src))
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        string newStem = modules.TryGetValue(stem, out string? m) ? m : stem;
        var sb = new StringBuilder();
        sb.AppendLine($"' {newStem}{ext} - decompiled Mordor source, with recovered names applied.");
        if (!string.Equals(newStem, stem, StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"' VBDis called this file {stem}{ext}; the program's own error strings name it {newStem}.");
        sb.AppendLine("' Generated by NameMap from names.tsv - do not edit by hand; edit the mapping and re-render.");
        sb.AppendLine("' The pristine decompiler output is in port/decompiled/.");
        sb.AppendLine("'");
        foreach (string line in File.ReadLines(path))
        {
            string outLine = line;
            if (tokenRe != null)
                outLine = tokenRe.Replace(line, mm => { hits++; return tokens[mm.Value].New; });
            sb.AppendLine(outLine);
        }
        File.WriteAllText(Path.Combine(outDir, newStem + ext), sb.ToString());
        files++;
    }
    Console.WriteLine($"wrote {files} files to {outDir}; {hits} identifier(s) replaced");
    return 0;
}

Console.Error.WriteLine("mode must be scan or apply");
return 1;

/// <summary>Which record Types each M-token belongs to, read from the Type blocks in MAIN.TXT.</summary>
static Dictionary<string, List<string>> MemberTypes(string src)
{
    var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    string? main = Directory.EnumerateFiles(src, "MAIN.TXT").FirstOrDefault();
    if (main == null) return map;
    string? type = null;
    foreach (string raw in File.ReadLines(main))
    {
        string line = raw.TrimEnd();
        if (line.StartsWith("Type ")) { type = line[5..].Trim(); continue; }
        if (line.StartsWith("End Type")) { type = null; continue; }
        if (type == null || !line.StartsWith("  M")) continue;
        string tok = line.Trim().Split(' ')[0];
        int paren = tok.IndexOf('(');
        if (paren > 0) tok = tok[..paren];
        if (!map.TryGetValue(tok, out var list)) map[tok] = list = new List<string>();
        if (!list.Contains(type)) list.Add(type);
    }
    return map;
}

static IEnumerable<string> Files(string dir) =>
    Directory.EnumerateFiles(dir, "*.BAS").Concat(Directory.EnumerateFiles(dir, "*.TXT"))
             .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
