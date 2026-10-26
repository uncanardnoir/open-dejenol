using Mordor.Core;

/// <summary>
/// Character passwords - <c>M594C</c>, and the three rules around it: an empty one is stored as a
/// single space, the prompt is skipped when the stored value right-trims to nothing, and the
/// comparison is case sensitive against that trimmed value.
/// </summary>
public static class PasswordProbe
{
    public static void Run(string dir)
    {
        RaceData.LoadFrom(dir); GuildData.LoadFrom(dir);
        Character Make(string pw)
        {
            var c = Character.Create("Tester", RaceData.All[0], Alignment.Neutral, Sex.Male,
                                     new[] { 12, 12, 12, 12, 12, 12 });
            c.Password = pw;
            return c;
        }

        Console.WriteLine("what gets stored, and whether the game asks:");
        Console.WriteLine($"  {"typed at creation",22} {"stored",10} {"asks?",6}");
        foreach (var (typed, stored) in new[]
                 {
                     ("(nothing)", Character.NoPassword),
                     ("swordfish", "swordfish"),
                     ("Swordfish", "Swordfish"),
                     ("  ", "  "),
                 })
        {
            var c = Make(stored);
            Console.WriteLine($"  {typed,22} {"[" + stored + "]",10} {(c.HasPassword ? "yes" : "no"),6}");
        }
        Console.WriteLine("  declining stores one space, which right-trims to nothing, so no prompt ever appears.");

        Console.WriteLine("\nthe comparison is case sensitive, against the right-trimmed stored value:");
        var guarded = Make("Swordfish");
        foreach (string tried in new[] { "Swordfish", "swordfish", "SWORDFISH", "Swordfish ", " Swordfish", "" })
            Console.WriteLine($"  typed [{tried}]".PadRight(28) + (guarded.PasswordMatches(tried) ? "in" : "Incorrect Password!"));

        Console.WriteLine("\na password with trailing spaces is trimmed when stored, so these are the same:");
        var trailing = Make("secret   ");
        foreach (string tried in new[] { "secret", "secret   " })
            Console.WriteLine($"  typed [{tried}]".PadRight(28) + (trailing.PasswordMatches(tried) ? "in" : "Incorrect Password!"));
        Console.WriteLine("  the original compares against RTrim(stored), so the spaces you typed are lost - and");
        Console.WriteLine("  typing them again fails, because the *entered* value is not trimmed.");
    }
}
