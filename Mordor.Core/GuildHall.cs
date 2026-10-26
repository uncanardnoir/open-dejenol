namespace Mordor.Core;

/// <summary>
/// What the Guild Hall says, from FRMCTYG.BAS. Almost none of it was here: the port had written
/// its own sentence for every one of these.
/// </summary>
public static class GuildHall
{
    // ---- the greeting that changes as you pick a guild out of the list (sub2962, 782-812) ----

    /// <summary>
    /// The line under the guild list. A member is greeted; a stranger is either turned away - for
    /// their race, their alignment, or their stats, and more than one of those can be said at once
    /// - or made welcome.
    ///
    /// The order is the routine's own: race first, then alignment, then stats, each appending to
    /// the last. "Welcome, Visitor." is only reached when none of the three had anything to say.
    ///
    /// The leading newline on the member greeting is in the original too: it writes
    /// <c>Chr$(13) + "Welcome, Brother."</c>, so the line sits one down from wherever the label
    /// starts.
    /// </summary>
    public static (string said, bool refused) Greeting(GuildDef def, Character c, bool alreadyAMember)
    {
        if (alreadyAMember) return ("\n" + (c.Sex == Sex.Female ? "Welcome, Sister." : "Welcome, Brother."), false);

        string said = "";
        bool refused = false;

        // "We don't accept Elfs!" - the plural is the race name with an s stuck on the end, which
        // is how the original builds it (FRMCTYG.BAS:790) and is wrong for at least two races.
        if (!def.AllowsRace(c.Race)) { said = $"We don't accept {c.Race}s!\n"; refused = true; }
        if (!def.AllowsAlignment(c.Alignment)) { said += $"You're {c.Alignment}!  Go away!\n"; refused = true; }
        for (int i = 0; i < 6; i++)
            if (c.Stats[i] < def.ReqStats[i]) { said += "Your natural stats are too low to join!"; refused = true; break; }

        return refused ? (said, true) : ("Welcome, Visitor.", false);
    }

    /// <summary>The Join button is captioned Re-Acquaint for a guild you already belong to.</summary>
    public static string JoinCaption(bool alreadyAMember) => alreadyAMember ? "Re-Acquaint" : "Join";

    /// <summary>
    /// How the guild list names each entry: the guild, and your level in it in brackets if you
    /// have one (sub2974, FRMCTYG.BAS:761-776).
    /// </summary>
    public static string ListRow(string guild, int levelOrZero) =>
        levelOrZero > 0 ? $"{guild} ({levelOrZero})" : guild;

    // ---- the Info box, cmdInfo_Click (FRMCTYG.BAS:45-81) ----

    /// <summary>
    /// The four lines of requirements, and the quest under them when there is one. The original
    /// shows this box by itself when you press Info, **and again** after either of the two Make
    /// Level refusals - which is why those refusals are so short: the numbers follow immediately.
    /// </summary>
    public static string Info(long experienceNeeded, long experienceHave, long goldNeeded, VbCurrency goldHave,
                             int questType, string questTarget, bool questDone)
    {
        string t = $"We require you to have {experienceNeeded:N0} experience to make the next level.\n"
                 + $"You currently have {experienceHave:N0} experience.\n"
                 + $"We also require {goldNeeded:N0} gold.\n"
                 + $"You currently have {CharStatFile.FormatNumber(goldHave)} gold.\n";
        if (questType > 0)
        {
            t += "\nYou have also been assigned a Quest to complete.\n";
            t += questType == 1
                ? $"You must kill one {questTarget}.\n"
                : $"You must return with one {questTarget} and present it when making level.\n";
            if (questDone) t += "Your quest has been completed.";
        }
        return t;
    }

    public const string InfoTitle = "Current Experience Information";

    // ---- making a level (sub28B4, FRMCTYG.BAS:223-310) ----

    /// <summary>Said on the message line, not in a box, when the guild has nothing left to teach.</summary>
    public const string NothingLeftToTeach = "You cannot learn any more from this Guild.";

    /// <summary>Both of these come up in a box headed "Make Level", with the Info box behind them.</summary>
    public const string NotEnoughExperience = "You do not currently have enough experience to make a level.";
    public const string NotEnoughGold = "You currently do not have enough gold to make a level.";
    public const string MakeLevelTitle = "Make Level";

    /// <summary>
    /// The one that makes it worth it, headed "MAKE LEVEL" (FRMCTYG.BAS:417). <c>Str$</c> supplies
    /// the space before the number.
    /// </summary>
    public static string MadeLevel(int level) => $"Congratulations!\nYou are now Level {level}!";

    // ---- joining (cmdJoin_Click, FRMCTYG.BAS:84-131) ----

    /// <summary>
    /// The warning that goes above the join question for a character with nothing behind them -
    /// <c>topGuildLevel(Me) &lt; 10</c>.
    /// </summary>
    public const string Warning =
        "A WARNING!\n"
      + "Your combined experience is below that which is recommended.\n"
      + "Our requirements for advancement may be too difficult for you at this point.\n\n";

    /// <summary>
    /// The question itself. Priced whenever you already belong to a guild other than the current
    /// one, even at a price of nought; asked plainly otherwise (FRMCTYG.BAS:115-130).
    /// </summary>
    public static string JoinQuestion(string guild, bool priced, long cost) =>
        priced ? $"Cost to join the {guild}'s guild\nis {cost:N0}.\nDo you wish to join?"
                 : $"Join the {guild}'s guild?";

    public const string JoinTitle = "JOIN GUILD";
}
