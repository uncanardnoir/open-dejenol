using System.Text;

namespace Mordor.Core;

/// <summary>
/// The guild log: MDATA9.MDR, a world-wide record of what every character has done, which each
/// guild hall shows filtered down to its own entries (FRMGLDL.BAS, and the writer
/// <c>sub0578</c> at MODULE11.BAS:25).
///
/// The original's file is a count followed by variable-length records, each one a guild index, the
/// day it happened, and the line of text. This keeps that shape and the same field order. The port
/// puts the file with the saves rather than beside the game data, because that is where everything
/// else the port writes already lives.
/// </summary>
public static class GuildLog
{
    /// <summary>mc0020: what a clean leaves behind, counted across the whole log, not per guild.</summary>
    public const int KeepOnClean = 50;

    public const string FileName = "MDATA9.MDR";
    public const string BackupName = "MDATA9B.MDR";
    public const string TextName = "GUILDLOG.TXT";

    /// <summary>One line of the log: which guild it belongs to, when, and what happened.</summary>
    public sealed record Entry(int Guild, long Day, string Text);

    public static string PathFor(string savesDir) => Path.Combine(savesDir, FileName);

    /// <summary>
    /// VB's <c>CLng(Date)</c>: whole days since 30 December 1899. The log stores this and the
    /// display subtracts today's, so the port has to count from the same epoch to say "3 DAYS AGO".
    /// </summary>
    public static long Today() => DaySerial(DateTime.Today);

    public static long DaySerial(DateTime d) => (long)(d.Date - new DateTime(1899, 12, 30)).TotalDays;

    public static DateTime FromDaySerial(long day) => new DateTime(1899, 12, 30).AddDays(day);

    // ---- reading ----

    /// <summary>Every entry, oldest first, as the file holds them. A missing log is an empty one.</summary>
    public static List<Entry> All(string savesDir)
    {
        var entries = new List<Entry>();
        string path = PathFor(savesDir);
        if (!File.Exists(path)) return entries;
        try
        {
            using var r = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
            if (r.BaseStream.Length < 4) return entries;
            r.ReadInt32();                                  // the header count; the records are the truth
            while (r.BaseStream.Position < r.BaseStream.Length)
            {
                int guild = r.ReadInt32();
                long day = r.ReadInt64();
                int len = r.ReadInt32();
                if (len < 0 || r.BaseStream.Position + len > r.BaseStream.Length) break;
                entries.Add(new Entry(guild, day, Encoding.UTF8.GetString(r.ReadBytes(len))));
            }
        }
        catch { }                                           // a damaged log loses its tail, not the game
        return entries;
    }

    /// <summary>One guild's entries, newest first - <c>lstLog.AddItem ..., 0</c> puts each new one on top.</summary>
    public static List<Entry> For(string savesDir, int guild) =>
        All(savesDir).Where(e => e.Guild == guild).AsEnumerable().Reverse().ToList();

    /// <summary>
    /// How the log dates a line: "TODAY: ", "1 DAY AGO: ", "5 DAYS AGO: ". The original takes the
    /// absolute difference, so a clock set backwards still reads as days ago rather than ahead.
    /// </summary>
    public static string Prefix(Entry e, long today)
    {
        long gap = Math.Abs(e.Day - today);
        if (e.Day == today) return "TODAY: ";
        return gap > 1 ? $"{gap} DAYS AGO: " : $"{gap} DAY AGO: ";
    }

    public static string Line(Entry e, long today) => Prefix(e, today) + e.Text;

    /// <summary>
    /// The Clean button only appears once the whole log holds more than fifty entries per guild
    /// (<c>Form_Resize</c>: <c>m001E > mc0020 * gv1BEA</c>).
    /// </summary>
    public static bool NeedsCleaning(string savesDir, int guildCount) =>
        All(savesDir).Count > KeepOnClean * Math.Max(1, guildCount);

    // ---- writing ----

    /// <summary>
    /// <c>sub0578</c>: bump the header count and append one record. Every call site names the
    /// character in quotes and ends with a full stop; see <see cref="Wrote"/> for the wording.
    /// </summary>
    public static void Append(string savesDir, int guild, string text)
    {
        try
        {
            Directory.CreateDirectory(savesDir);
            var entries = All(savesDir);
            entries.Add(new Entry(guild, Today(), text));
            WriteAll(savesDir, entries);
        }
        catch { }                                           // a log that cannot be written is not fatal
    }

    /// <summary>Replace the whole log. Only the smoke-test hooks need this, to plant dated entries.</summary>
    public static void Rewrite(string savesDir, List<Entry> entries)
    {
        try { Directory.CreateDirectory(savesDir); WriteAll(savesDir, entries); } catch { }
    }

    private static void WriteAll(string savesDir, List<Entry> entries)
    {
        using var w = new BinaryWriter(File.Create(PathFor(savesDir)), Encoding.UTF8);
        w.Write(entries.Count);
        foreach (var e in entries)
        {
            var bytes = Encoding.UTF8.GetBytes(e.Text);
            w.Write(e.Guild);
            w.Write(e.Day);
            w.Write(bytes.Length);
            w.Write(bytes);
        }
    }

    /// <summary>
    /// <c>sub316B</c>: keep the newest fifty entries of the whole log and throw the rest away,
    /// optionally printing them to GUILDLOG.TXT first. The old file is copied to MDATA9B.MDR
    /// before anything is touched. Returns how many entries were removed.
    /// </summary>
    public static int Clean(string savesDir, string doneBy, Func<int, string> guildName, bool writeText)
    {
        var entries = All(savesDir);
        if (entries.Count <= KeepOnClean) return 0;

        int cut = entries.Count - KeepOnClean;
        var discarded = entries.Take(cut).ToList();
        var kept = entries.Skip(cut).ToList();

        try { File.Copy(PathFor(savesDir), Path.Combine(savesDir, BackupName), overwrite: true); } catch { }

        if (writeText)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(" ");
                sb.AppendLine($"BEGIN GUILD LOG CLEAN ({DateTime.Today:d})");
                sb.AppendLine($"Done By: \"{doneBy}\"");
                sb.AppendLine(" ");
                foreach (var e in discarded)
                {
                    sb.AppendLine("GUILD: " + guildName(e.Guild));
                    sb.AppendLine(" DATE: " + FromDaySerial(e.Day).ToString("d"));
                    sb.AppendLine("  LOG: " + e.Text);
                }
                File.AppendAllText(Path.Combine(savesDir, TextName), sb.ToString());
            }
            catch { }
        }

        WriteAll(savesDir, kept);
        return discarded.Count;
    }

    // ---- the entries the game writes, in the original's own words ----

    /// <summary>
    /// Every line the game logs, worded as the original words it. The call sites are CHARCREA.BAS:258,
    /// FRMCTYG.BAS:148/323/439/563/565/726/955/957, MODULE2.BAS:638, MODULE3.BAS:1631,
    /// MODULE4.BAS:4271/4274, MODULE6.BAS:368/3105/4575/4577 and MODULE11.BAS:511.
    /// </summary>
    public static class Wrote
    {
        private static string Q(string name) => "\"" + name.TrimEnd() + "\"";

        public static string JoinedGuild(string who) => Q(who) + " joined our guild.";

        public static string Titled(string who, string title, int level) =>
            Q(who) + " was appointed the title of \"" + title + "\" for reaching Level " + level + ".";

        public static string BecameGuildmaster(string who) => Q(who) + " became the new GuildMaster.";

        /// <summary>MODULE2.BAS:638 - the guild appoints an absent master when the game starts up.</summary>
        public static string AppointedGuildmaster(string who) => Q(who) + " was appointed GuildMaster.";

        public static string QuestForItem(string who, string item) =>
            Q(who) + " was assigned a quest to return with " + item + ".";

        public static string QuestForMonster(string who, string monster) =>
            Q(who) + " was assigned a quest to kill one " + monster + ".";

        public static string GuildmasterAward(string who, string item) =>
            Q(who) + " was awarded the '" + item + "' for achieving the rank of new Guildmaster.";

        /// <summary>The original writes "his" or "her" from the character's sex.</summary>
        public static string Demoted(string who, int levels, Sex sex) =>
            Q(who) + " was demoted by " + levels + " levels for not completing " + (sex == Sex.Male ? "his" : "her") + " quest.";

        public static string SetRecord(string who, string record, string value) =>
            Q(who) + " set the record for " + record + " with a value of " + value + ".";

        public static string FirstToLevel(string who, int level) =>
            Q(who) + " was the first to go to level " + level + ".";

        public static string PartyBeatAsmodeus(string who) =>
            Q(who) + " was part of the party that defeated Asmodeus.";

        public static string AloneBeatAsmodeus(string who) =>
            Q(who) + " single handedly defeated Asmodeus.";

        public static string Resurrected(string who, string by) =>
            Q(who) + " was resurrected by " + Q(by) + ".";

        public static string ResurrectedInMorgue(string who) => Q(who) + " was resurrected in the morgue.";

        public static string DiedOnLevel(string who, int level) => Q(who) + " died on level " + level + ".";

        public static string DiedSomewhere(string who) => Q(who) + " died somewhere in the dungeon.";
    }
}
