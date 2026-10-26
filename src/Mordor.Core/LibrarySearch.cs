using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Mordor.Core;

/// <summary>
/// The Library's list and its Search box (FRMLIBRA.BAS). lstList is a Sorted ListBox in the form
/// data (MORDOR.EXE RCDATA 56), so the names sub327E adds in the data's order are shown
/// alphabetically. txtSearch_KeyDown (FRMLIBRA.BAS:130-156): on Enter, every entry of the list now
/// shown whose start is the typed text, compared in upper case, is counted, and the last one kept.
/// None: "No match was found."; one: it is selected and the box cleared; more: "ambiguous!". Both
/// boxes are MsgBox 64 titled "Search", and after either the box's text is selected.
/// </summary>
public static class LibrarySearch
{
    public const string Title = "Search";
    public const string NoMatch = "No match was found.";
    public const string Ambiguous = "ambiguous!";

    /// <summary>What Enter in the Search box does.</summary>
    public enum Outcome { NoMatch, Selected, Ambiguous }

    /// <summary>
    /// The search over <paramref name="list"/>, in the order it is shown: the outcome, and for one
    /// match the index to select. <c>UCase(Left(entry, Len(typed))) = UCase(typed)</c>: an empty box
    /// matches every entry.
    /// </summary>
    public static (Outcome Outcome, int Index) Find(IReadOnlyList<string> list, string typed)
    {
        int count = 0, last = -1;
        for (int i = 0; i < list.Count; i++)
        {
            string entry = list[i];
            string start = entry.Length > typed.Length ? entry[..typed.Length] : entry;
            if (string.Equals(start.ToUpperInvariant(), typed.ToUpperInvariant(), StringComparison.Ordinal))
            {
                last = i;
                count++;
            }
        }
        return count switch
        {
            0 => (Outcome.NoMatch, -1),
            1 => (Outcome.Selected, last),
            _ => (Outcome.Ambiguous, last),
        };
    }

    /// <summary>
    /// The order a Sorted ListBox shows its entries in: Windows' LBS_SORT compares as lstrcmpi does,
    /// without regard to case and by "word sort", in which a hyphen and an apostrophe count for
    /// nothing at the first level - so Archer, Arch-Fiend, Archmage and Dragonfly, Dragons,
    /// Dragon's Blood, where .NET's own culture compare (ICU) would put the hyphen and the
    /// apostrophe first. Stable, so names that compare equal keep the order they were added in.
    /// </summary>
    public static List<T> Sorted<T>(IEnumerable<T> rows, Func<T, string> name) =>
        rows.OrderBy(name, ListBoxOrder.Instance).ToList();

    /// <summary>
    /// lstrcmpi's order. On Windows it is lstrcmpiW itself (CompareStringW in the thread's locale,
    /// NORM_IGNORECASE, a word sort); elsewhere <see cref="WordSort"/>, which stands in for it.
    /// </summary>
    public sealed class ListBoxOrder : IComparer<string>
    {
        public static readonly ListBoxOrder Instance = new();

        public int Compare(string? a, string? b)
        {
            a ??= "";
            b ??= "";
            if (OperatingSystem.IsWindows())
            {
                try { return Math.Sign(lstrcmpiW(a, b)); }
                catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
            }
            return WordSort(a, b);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, ExactSpelling = true)]
        private static extern int lstrcmpiW(string a, string b);
    }

    /// <summary>
    /// A managed stand-in for lstrcmpi's word sort: hyphens and apostrophes left out, the rest
    /// compared without regard to case; names equal that way are told apart by the characters
    /// left out; names that differ in case alone compare equal, as lstrcmpi's do.
    /// </summary>
    public static int WordSort(string a, string b)
    {
        static string Words(string s) => s.Replace("-", "").Replace("'", "");
        var ci = CultureInfo.InvariantCulture.CompareInfo;
        int c = ci.Compare(Words(a), Words(b), CompareOptions.IgnoreCase);
        if (c != 0) return Math.Sign(c);
        c = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return Math.Sign(c);
    }
}
