using System;
using Avalonia.Controls;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// <c>sub16CC</c> (MAINMOD2.BAS:5914-5959), the party's unload on the way back to the main menu.
/// sub1738 runs it once the game loop has ended with M6023 set (MAINMOD2.BAS:6322-6325) - the
/// dungeon's <c>q</c> and Exit button (3619-3627) and the city's Exit (FRMCTYM.BAS:214-219) - and
/// frmMain's close runs it too while a game is on (FRMMAIN.BAS:179-181), a way out the port refuses.
///
/// With anyone beside the leader it says "Disbanding Party..." and asks whether the party should
/// come back with the leader; Yes writes the party file's record, No blanks it (5926-5934). A leader
/// on their own has their record blanked without a question (5936-5937). The next time that leader
/// is run, mdrmulLoadParty reads the record back (MAINMOD2.BAS:6306; MULTIMOD.BAS:201-274).
/// </summary>
public static class PartyUnload
{
    public static async System.Threading.Tasks.Task Run(Window owner, Party party, string savesDir, Action<string> say)
    {
        if (party.Size > 1)   // UBound(Chars) > False (MAINMOD2.BAS:5926)
        {
            say(Party.Disbanding);
            // style mc0816 & mc0818 is VB's string join, "324": Yes/No with No the default button
            // (MAINMOD2.BAS:5928), as OriginalBox.YesNoDefaultNo explains
            if (await OriginalBox.YesNoDefaultNo(owner, Party.ReloadTitle, party.ReloadQuestion))
            {
                // MsgBox "Error saving party!", 48, "Error!" when fn179B answers False (MAINMOD2.BAS:5929-5931)
                if (!party.SaveParty(savesDir, remember: true, CrashLog.Note))
                    await OriginalBox.Tell(owner, "Error!", "Error saving party!");
            }
            else party.SaveParty(savesDir, remember: false, CrashLog.Note);   // MAINMOD2.BAS:5933
        }
        else party.SaveParty(savesDir, remember: false, CrashLog.Note);       // MAINMOD2.BAS:5937

        // MAINMOD2.BAS:5948-5953: the leader marked Preload Character (M5914(3)) is written as
        // [Loading Information] Character, which mdrReadAllData loads at the next start
        // (MAINMOD.BAS:1266-1272); a leader not marked has the entry blanked only if it names them.
        // The original writes MORDOR.INI; the port keeps it in the saves directory's settings.ini,
        // beside NotFirstTime, and never touches MORDOR.INI.
        string leader = party.Leader.Name.TrimEnd();
        // extfn0184's answer is thrown away (l0812); a folder that cannot be written is noted in the
        // port's log and the unload carries on
        try
        {
            bool wrote = true;
            if (party.Leader.Options.PreloadCharacter) wrote = PortSettings.Write(savesDir, "Character", leader);
            else if (PortSettings.Load(savesDir).Character == leader) wrote = PortSettings.Write(savesDir, "Character", "");
            if (!wrote) CrashLog.Note("could not write Character to " + PortSettings.PathFor(savesDir));
        }
        catch (Exception ex) { CrashLog.Note("could not write Character to settings.ini: " + ex.Message); }
    }
}
