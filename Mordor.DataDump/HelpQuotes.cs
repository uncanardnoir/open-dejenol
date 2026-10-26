using System.Text.RegularExpressions;

/// <summary>
/// Every passage this project quotes from the original's help, checked against the help itself.
///
/// FIDELITY leans on the help for rules the source alone does not settle - why the window layout
/// goes stale, what perception is for, which commands work in town - and quotes it directly. A
/// quotation is a claim like any other, and until now nothing confirmed that the words attributed
/// to the help are the help's.
///
/// The help is the game folder's MORDOR.HLP, read as the app reads it (HelpText). It is
/// flattened once, whitespace collapsed, so that a quotation spanning a line break still matches.
///
/// Elisions are respected. A quotation containing "..." is split there and each piece has to
/// appear, in order, which is what the ellipsis claims and no more.
/// </summary>
public static class HelpQuotes
{
    /// <summary>The whole help, titles and text, as one line.</summary>
    public static string Flatten(string gameDir)
    {
        var all = new System.Text.StringBuilder();
        foreach (var t in HelpText.Topics(gameDir)) all.Append(' ').Append(t.Title).Append(' ').Append(t.Text);
        return Regex.Replace(all.ToString(), @"\s+", " ");
    }

    /// <summary>
    /// A quoted passage attributed to the help. Only passages long enough to be a quotation rather
    /// than a word or a label are taken - twenty-five characters - and only where the word "help"
    /// appears just before them, so that the file's many other quotations are left alone.
    /// </summary>
    public static List<string> QuotesIn(string docPath)
    {
        string text = File.ReadAllText(docPath);
        var found = new List<string>();
        foreach (Match m in Regex.Matches(text, @"[Hh]elp[^.""\n]{0,40}""([^""\n]{25,300})"""))
            found.Add(m.Groups[1].Value);
        return found;
    }

    /// <summary>
    /// Everything about a quotation that the help does not carry. An ellipsis splits it, and each
    /// piece must appear in order - a quotation is only claiming the pieces, not the join.
    /// </summary>
    public static List<string> Missing(IEnumerable<string> quotes, string flat)
    {
        var bad = new List<string>();
        foreach (string q in quotes)
        {
            var pieces = Regex.Split(q, @"\s*\.\.\.\s*")
                              .Select(p => Regex.Replace(p.Trim(), @"\s+", " "))
                              .Where(p => p.Length >= 12).ToList();
            if (pieces.Count == 0) continue;

            int from = 0; bool ok = true;
            foreach (string p in pieces)
            {
                int at = flat.IndexOf(p, from, StringComparison.OrdinalIgnoreCase);
                if (at < 0) { ok = false; break; }
                from = at + p.Length;
            }
            if (!ok) bad.Add(q.Length > 70 ? q[..70] + "..." : q);
        }
        return bad;
    }

    public static void Run(string dir)
    {
        string root = PortTree.Root;
        string doc = Path.Combine(root, "docs", "FIDELITY.md");
        if (HelpText.Topics(dir).Count == 0 || !File.Exists(doc))
        { Console.WriteLine("help or FIDELITY.md missing"); return; }

        string flat = Flatten(dir);
        var quotes = QuotesIn(doc);
        var missing = Missing(quotes, flat);

        Console.WriteLine($"{HelpText.Topics(dir).Count} help topics (MORDOR.HLP), "
                        + $"{flat.Length:N0} characters of text");
        Console.WriteLine($"{quotes.Count} passages attributed to the help, {missing.Count} not found\n");
        foreach (string m in missing) Console.WriteLine("  MISSING  " + m);
        if (missing.Count == 0) Console.WriteLine("  every quotation is the help's own words");
    }
}
