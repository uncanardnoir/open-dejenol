using Mordor.Modding;

namespace Mordor.Mod.ICloudSaves;

/// <summary>
/// iCloud Saves: copies of the saved world kept in the player's iCloud Drive, so a game can be carried
/// on on another Mac (docs/MOBILE_AND_CLOUD.md, Phase M3; docs/MODDING.md, "The iCloud Saves mod").
/// The game's own saving is untouched: this only copies the saves folder up at the game's save points
/// and, when asked at the start, back down (<see cref="CloudSaves"/>, <see cref="CloudSync"/>).
///
/// macOS only. Anywhere else it loads, logs that it has nothing to do, and hooks nothing - so it
/// changes nothing on Windows even when ticked on. Off by default, as every mod is.
///
/// No Harmony (whose patches do not apply on arm64 macOS): it needs only the mod API, two parts of
/// which were added for it in API version 15 - <see cref="ModEvents.Saved"/> (the saves are on disk at
/// a save point) and <see cref="ModActions.MainMenuOpened"/> (the main menu at start, nothing loaded).
///
/// The window is reached only through <see cref="Draw"/>, so loading the mod needs no Avalonia: the
/// game has it, the headless checks do not.
/// </summary>
public sealed class ICloudSavesMod : IMod, IModLifecycle
{
    /// <summary>The window's title, and its toolbar button's.</summary>
    public const string WindowTitle = CloudSaves.Title;

    /// <summary>The mod's behaviour, once loaded on a Mac; null elsewhere.</summary>
    public CloudSaves? Saves { get; private set; }

    public void Load(IModContext ctx)
    {
        if (!OperatingSystem.IsMacOS())
        {
            ctx.Log.Info("macOS only: nothing to do on this system, so nothing is hooked");
            return;
        }
        var settings = ctx.Settings;
        void Report(string what, Exception? ex) { try { ctx.Log.Error(what, ex); } catch { } }
        var saves = new CloudSaves(ctx.DataDirectory, ICloudDrive.Locate, Environment.MachineName,
                                   () => settings.Get("UploadAtSavePoints"), () => settings.Get("CheckAtStart"),
                                   line => { try { ctx.Log.Info(line); } catch { } }, Report);
        Saves = saves;
        ctx.Events.Saved += p => saves.SavePoint(p.Kind);
        ctx.Actions.MainMenuOpened = saves.MainMenu;
        ctx.UI.AddWindow(WindowTitle, () => Draw.View(saves, Report));
        ctx.Log.Info($"ready: iCloud Drive at {ICloudDrive.CloudDocs}, saves at {ctx.DataDirectory}");
    }

    /// <summary>Switched on again on the main menu: nothing to restart - the hooks were wired once in Load.</summary>
    public void OnEnable() { }

    /// <summary>Switched off: nothing is held open or waiting; every write is finished before its call returns.</summary>
    public void OnDisable() { }

    /// <summary>The way to <see cref="CloudSavesView"/>, typed as object and never inlined (see the class's remarks).</summary>
    private static class Draw
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static object View(CloudSaves saves, Action<string, Exception?> report) => CloudSavesView.Build(saves, report);
    }
}
