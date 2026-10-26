using System;
using System.Collections.Generic;
using System.Linq;
using Mordor.Core;
using Mordor.Modding;

namespace Mordor.App;

/// <summary>
/// Starts the mod host (docs/MODDING.md). Mods are looked for in <c>mods\</c> beside the game
/// folder - beside <c>saves\</c>, whose settings.ini says which are switched on - and in
/// <c>mods\</c> beside the program, where the build puts the mods that come with the port;
/// <c>MORDOR_MODS</c> replaces both. Only enabled mods are loaded, and none is enabled until the
/// player ticks it on the Mods page (the Mods button of the main menu or the Control Panel).
/// </summary>
internal static class ModStartup
{
    public static void Start(string gameDir)
    {
        try
        {
            string settings = PortSettings.PathFor(Saves.DirFor(gameDir));
            // Every line - what loaded, each Harmony patch, a mod's own Info - goes to mods.log beside
            // settings.ini, begun afresh each start; only the problems go to crash.log as well, so a
            // start with mods that work leaves crash.log alone (docs/MODDING.md, Safeguards)
            string modsLog = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settings) ?? ".", "mods.log");
            bool begun = false;
            var gate = new object();
            void Log(string line)
            {
                lock (gate) try
                {
                    string text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {line}{Environment.NewLine}";
                    if (!begun) System.IO.File.WriteAllText(modsLog, text);
                    else System.IO.File.AppendAllText(modsLog, text);
                    begun = true;
                }
                catch { /* a log that cannot be written is no reason to stop */ }
            }
            var host = new ModHost(new ModSettingsStore(settings), Log, CrashLog.Note);
            host.Discover(ModHost.DefaultRoots(gameDir, AppContext.BaseDirectory));
            host.LoadEnabled();
            // the game's "Cast on Who?" box for a mod's window (ModGame.AskCastOnWho)
            host.CastOnWhoBox = PickList.ForMods;
            ModHost.Active = host;
            Saves.ModTag = host.SaveTag;
        }
        catch (Exception ex)
        {
            // the loader itself failing must not stop the game: it runs as if there were no mods
            CrashLog.Write("starting the mods", ex);
            ModHost.Active = ModHost.Empty();
            Saves.ModTag = null;
        }
    }

    /// <summary>Characters already warned about this session, so each is told once.</summary>
    private static readonly HashSet<string> Warned = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A save made with mods that are not running now: say so, once per character per session,
    /// with an OK box. The character loads either way.
    /// </summary>
    public static async System.Threading.Tasks.Task WarnIfModsMissing(Avalonia.Controls.Window owner, Character ch)
    {
        var missing = ModHost.Active.MissingFrom(ch.ModsWhenLoaded);
        if (missing.Count == 0 || !Warned.Add(ch.Name.TrimEnd())) return;
        await OriginalBox.Tell(owner, "Mods",
            $"{ch.Name.TrimEnd()} was last saved with these mods running, which are not running now:\n\n"
            + string.Join("\n", missing.Select(m => "  " + ModHost.Active.Describe(m))) + "\n\n"
            + "The character loads as it is. Whatever those mods changed stays changed, but they will not "
            + "go on doing it. Mods are switched on from the Mods button on the main menu or the Control Panel.");
    }
}
