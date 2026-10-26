using System.Text;
using System.Text.RegularExpressions;

// Translates the slice of MODULE3.BAS (MAZEMOD) that decides what the dungeon view shows into C#.
//
// This is not a general VB translator. It handles exactly the shapes that appear in sub0980's
// selection logic: single-line and block If, For/Next, Select Case, GoTo and labels, and
// assignments to the view arrays. Anything it does not recognise is emitted as a comment prefixed
// with UNTRANSLATED so it cannot pass silently.
//
// Original identifiers are kept wherever they are locals or constants, so that every generated
// line can be diffed against the decompiled source. Only the three arrays and the three character
// fields are renamed, because those are what the hand-written half of MazeSelect has to agree on.
//
// usage: VbToCs <input.vb> <output.cs> <MethodName> [--entry]

if (args.Length < 3) { Console.Error.WriteLine("usage: VbToCs <in.vb> <out.cs> <Method> [--entry]"); return 1; }
string[] lines = File.ReadAllLines(args[0]);
string method = args[2];
bool entry = args.Contains("--entry");

var body = new StringBuilder();
int indent = 2;
int untranslated = 0, translated = 0;
string? pendingCase = null;

string Pad() => new string(' ', indent * 4);

// ---- expression rewriting ------------------------------------------------------------------
// The array subscripts can themselves contain a call, so the subscript patterns allow one level
// of nested parentheses.
const string Sub1 = @"\(([^()]*(?:\([^()]*\)[^()]*)*)\)";

string Expr(string s)
{
    s = s.Trim();
    s = Regex.Replace(s, @"v\?01FE" + Sub1, "V[$1]");     // the 22-square view frustum
    s = Regex.Replace(s, @"gv1B72" + Sub1, "Slot[$1]");   // the eight overlay slots
    s = Regex.Replace(s, @"m01D2" + Sub1, "Bg[$1]");      // the two background halves
    s = s.Replace("gv14C2(m00C0).M583F", "px")            // the party leader's x, y and facing
         .Replace("gv14C2(m00C0).M5845", "py")
         .Replace("gv14C2(m00C0).M5899", "facing");
    s = Regex.Replace(s, @"\bfn0380\(", "Bit(");          // MODULE1.BAS:211, bit test
    s = Regex.Replace(s, @"\bfn0418\(", "SetBit(");       // MODULE1.BAS:1017, bit set
    s = Regex.Replace(s, @"\bfn0AD3\(", "WallsAt(v, ");   // MODULE3.BAS:61, wall word of a square
    // comparisons and logic
    s = Regex.Replace(s, @"(?<![<>!])=(?!=)", "==");
    s = s.Replace("<==", "<=").Replace(">==", ">=").Replace("<>", "!=");
    s = Regex.Replace(s, @"\bAnd\b", "&&");
    s = Regex.Replace(s, @"\bOr\b", "||");
    s = Regex.Replace(s, @"\bNot\b", "!");
    s = Regex.Replace(s, @"\bFalse\b", "0");
    s = Regex.Replace(s, @"\bTrue\b", "1");
    return s;
}

// ---- conditions ------------------------------------------------------------------------------
// VB has no boolean type here: a comparison yields -1 or 0, And/Or are bitwise, and If tests for
// non-zero. For a chain of plain comparisons that is indistinguishable from C# && and ||, so the
// common case translates literally. Two shapes in this slice are not plain comparisons and are
// handled explicitly; anything else returns null and becomes an UNTRANSLATED comment.

string[] CmpOps = { "<>", ">=", "<=", "=", ">", "<" };

string CsOp(string op) => op switch { "=" => "==", "<>" => "!=", _ => op };

// And/Or operands at paren depth zero, with the joiners between them
List<string> SplitOps(string s, List<string> joiners)
{
    var parts = new List<string>();
    int depth = 0, start = 0;
    for (int i = 0; i < s.Length; i++)
    {
        if (s[i] == '(') { depth++; continue; }
        if (s[i] == ')') { depth--; continue; }
        if (depth != 0) continue;
        foreach (string kw in new[] { "And", "Or" })
        {
            if (i + kw.Length > s.Length || string.CompareOrdinal(s, i, kw, 0, kw.Length) != 0) continue;
            if (i > 0 && (char.IsLetterOrDigit(s[i - 1]) || s[i - 1] == '_')) continue;
            int after = i + kw.Length;
            if (after < s.Length && (char.IsLetterOrDigit(s[after]) || s[after] == '_')) continue;
            parts.Add(s[start..i]);
            joiners.Add(kw);
            start = after;
            i = after - 1;
            break;
        }
    }
    parts.Add(s[start..]);
    return parts;
}

// comparison operators in one operand, at paren depth zero
List<(int Pos, string Op)> FindCmps(string s)
{
    var found = new List<(int, string)>();
    int depth = 0;
    for (int i = 0; i < s.Length; i++)
    {
        if (s[i] == '(') { depth++; continue; }
        if (s[i] == ')') { depth--; continue; }
        if (depth != 0) continue;
        foreach (string op in CmpOps)
        {
            if (i + op.Length > s.Length || string.CompareOrdinal(s, i, op, 0, op.Length) != 0) continue;
            found.Add((i, op));
            i += op.Length - 1;
            break;
        }
    }
    return found;
}

string? Cond(string vb)
{
    if (Regex.IsMatch(vb, @"\bNot\b")) return null;
    var joiners = new List<string>();
    var operands = SplitOps(vb, joiners);
    var done = new List<string>();
    int bare = 0;

    foreach (string raw in operands)
    {
        string o = raw.Trim();
        var cmps = FindCmps(o);
        if (cmps.Count == 0)
        {
            // a raw value used as a truth test, e.g. "And v?01FE(7)"
            bare++;
            done.Add($"{Expr(o)} != 0");
        }
        else if (cmps.Count == 1)
        {
            var (pos, op) = cmps[0];
            done.Add($"{Expr(o[..pos])} {CsOp(op)} {Expr(o[(pos + op.Length)..])}");
        }
        else if (cmps.Count == 2)
        {
            // comparisons fold left and yield -1 for true, so "a > 0 = 0" asks whether a > 0 is
            // False. Rendering the inner result as -1 or 0 keeps that exact.
            var (p1, op1) = cmps[0];
            var (p2, op2) = cmps[1];
            string a = Expr(o[..p1]);
            string b = Expr(o[(p1 + op1.Length)..p2]);
            string c = Expr(o[(p2 + op2.Length)..]);
            done.Add($"(({a} {CsOp(op1)} {b}) ? -1 : 0) {CsOp(op2)} {c}");
        }
        else return null;
    }

    // With one raw value in the chain, bitwise And/Or still agrees with && and ||, because every
    // other operand is -1 or 0 and the test is for non-zero. With two it would not, so refuse.
    if (bare > 1) return null;

    var sb = new StringBuilder(done[0]);
    for (int i = 0; i < joiners.Count; i++)
        sb.Append(joiners[i] == "And" ? " && " : " || ").Append(done[i + 1]);
    return sb.ToString();
}

string Assign(string s)
{
    s = s.Trim();
    var m = Regex.Match(s, @"^(.*?)\s*=\s*(.*)$");
    if (!m.Success) return "";
    string lhs = Expr(m.Groups[1].Value).Replace("==", "=");
    string rhs = Expr(m.Groups[2].Value);
    return $"{lhs} = {rhs};";
}

// ---- line by line --------------------------------------------------------------------------
foreach (string raw in lines)
{
    string t = raw.Trim();
    if (t.Length == 0) { body.AppendLine(); continue; }
    if (t.StartsWith('\'')) { body.AppendLine($"{Pad()}//{t[1..]}"); continue; }

    var lab = Regex.Match(t, @"^(L[0-9A-F]+):$");
    if (lab.Success) { body.AppendLine($"{Pad()}{lab.Groups[1].Value}: ;"); translated++; continue; }

    if (t == "PRUNECALL") { body.AppendLine($"{Pad()}Prune(v);"); translated++; continue; }

    var jmp = Regex.Match(t, @"^GoTo\s+(L[0-9A-F]+)$");
    if (jmp.Success) { body.AppendLine($"{Pad()}goto {jmp.Groups[1].Value};"); translated++; continue; }

    if (t == "End If") { indent--; body.AppendLine($"{Pad()}}}"); translated++; continue; }
    if (t == "End Select")
    {
        if (pendingCase != null) { body.AppendLine($"{Pad()}    break;"); pendingCase = null; }
        indent--; body.AppendLine($"{Pad()}}}"); translated++; continue;
    }
    if (Regex.IsMatch(t, @"^Next\b")) { indent--; body.AppendLine($"{Pad()}}}"); translated++; continue; }

    var sel = Regex.Match(t, @"^Select Case\s+(.*)$");
    if (sel.Success)
    {
        body.AppendLine($"{Pad()}switch ({Expr(sel.Groups[1].Value)})");
        body.AppendLine($"{Pad()}{{");
        indent++; translated++; continue;
    }

    var cas = Regex.Match(t, @"^Case\s+(.*)$");
    if (cas.Success)
    {
        if (pendingCase != null) body.AppendLine($"{Pad()}    break;");
        string what = cas.Groups[1].Value.Trim();
        body.AppendLine(what.Equals("Else", StringComparison.OrdinalIgnoreCase)
            ? $"{Pad()}default:"
            : $"{Pad()}case {Expr(what)}:");
        pendingCase = what; translated++; continue;
    }

    var fr = Regex.Match(t, @"^For\s+(\S+)\s*=\s*(.*?)\s+To\s+(.*)$");
    if (fr.Success)
    {
        // the loop variable is one of the procedure's Statics, declared in the preamble
        string v = fr.Groups[1].Value.Trim();
        body.AppendLine($"{Pad()}for ({v} = {Expr(fr.Groups[2].Value)}; {v} <= {Expr(fr.Groups[3].Value)}; {v}++)");
        body.AppendLine($"{Pad()}{{");
        indent++; translated++; continue;
    }

    // If <cond> Then            (block)
    // If <cond> Then <stmts>    (single line, statements separated by colons)
    var iff = Regex.Match(t, @"^If\s+(.*?)\s+Then\s*(.*)$");
    if (iff.Success)
    {
        string? cond = Cond(iff.Groups[1].Value);
        string tail = iff.Groups[2].Value.Trim();
        if (cond is null) { body.AppendLine($"{Pad()}// UNTRANSLATED: {t}"); untranslated++; continue; }
        if (tail.Length == 0)
        {
            body.AppendLine($"{Pad()}if ({cond})");
            body.AppendLine($"{Pad()}{{");
            indent++; translated++; continue;
        }
        var stmts = tail.Split(':', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (stmts.All(s => s.StartsWith("GoTo") || s.Contains('=')))
        {
            body.AppendLine($"{Pad()}if ({cond}) {{ " +
                string.Join(" ", stmts.Select(s => s.StartsWith("GoTo")
                    ? $"goto {s[5..].Trim()};" : Assign(s))) + " }");
            translated++; continue;
        }
    }

    if (Regex.IsMatch(t, @"^Else\b"))
    {
        indent--; body.AppendLine($"{Pad()}}}"); body.AppendLine($"{Pad()}else"); body.AppendLine($"{Pad()}{{");
        indent++; translated++; continue;
    }

    // a bare assignment, or several separated by colons
    var parts = t.Split(':', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())
                 .Where(s => s.Length > 0).ToList();
    if (parts.Count > 0 && parts.All(s => s.Contains('=')))
    {
        var done = parts.Select(Assign).ToList();
        if (done.All(s => s.Length > 0))
        {
            foreach (string d in done) body.AppendLine($"{Pad()}{d}");
            translated++; continue;
        }
    }

    body.AppendLine($"{Pad()}// UNTRANSLATED: {t}");
    untranslated++;
}

var outText = new StringBuilder();
outText.AppendLine("// <auto-generated>");
outText.AppendLine($"// Translated from {Path.GetFileName(args[0])} by tools/VbToCs. Do not edit by hand -");
outText.AppendLine("// change the translator or the source slice and regenerate. See MazeSelect.cs.");
outText.AppendLine("// </auto-generated>");
outText.AppendLine("#pragma warning disable CS0164  // a label the original jumps to from a branch we translated away");
outText.AppendLine("namespace Mordor.Core;");
outText.AppendLine();
outText.AppendLine("public static partial class MazeSelect");
outText.AppendLine("{");
outText.AppendLine($"    private static void {method}(View v)");
outText.AppendLine("    {");
outText.AppendLine("        int[] V = v.V; int[] Slot = v.Slot; int[] Bg = v.Bg;");
if (entry)
{
    outText.AppendLine("        int px = v.X, py = v.Y, facing = v.Facing;");
    outText.AppendLine("        // sub0980 keeps these as Statics. m01C0 is assigned at MODULE3.BAS:784");
    outText.AppendLine("        // from fn0AE8 - the linear index of the party leader's square. The rest are");
    outText.AppendLine("        // written before they are read on every path through the slice.");
    outText.AppendLine("        int m01B4 = 0, m01B6 = 0, m01B8 = 0, m01BA = 0, m01BC = 0, m01C4 = 0;");
    outText.AppendLine("        int m01C0 = v.Square;");
}
outText.Append(body);
outText.AppendLine("    }");
outText.AppendLine("}");
File.WriteAllText(args[1], outText.ToString());

Console.WriteLine($"{Path.GetFileName(args[1])}: translated {translated} statements, {untranslated} UNTRANSLATED");
return untranslated > 0 ? 2 : 0;
