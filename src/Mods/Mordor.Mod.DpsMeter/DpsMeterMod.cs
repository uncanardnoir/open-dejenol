using Mordor.Modding;

namespace Mordor.Mod.DpsMeter;

/// <summary>
/// DPS Meter: a window with each party member's damage dealt - Physical (their weapon's blows),
/// Magical (their damage spells, their own or an item's), Companion (their companions' breath,
/// spit, blows, stone, shocks and spells), the total - and their kills, counted from
/// <see cref="ModEvents.DamageDealt"/>; and, in its XP mode, the experience each gained - from damage
/// dealt and from chests' traps disarmed - from <see cref="ModEvents.ExperienceGained"/> (API version
/// 8): what the total actually went up by, after the clamp at the pin. Both are kept in
/// dpsmeter.json (<see cref="DpsTally"/>) until the window's Reset.
///
/// The figures are the true ones, shown by the game or not: the hits each actually took off a
/// monster, capped at what it had left (an overkill is not counted). This mod is not held to the
/// Quality of Life mod's "reveal nothing" rule (the user, 2026-10-21). It changes nothing in the
/// game: it only listens.
///
/// The window is reached only through <see cref="Draw"/>, so loading the mod needs no Avalonia:
/// the game has it, the headless probe does not.
/// </summary>
public sealed class DpsMeterMod : IMod, IModLifecycle
{
    /// <summary>
    /// Switched on again on the main menu (API version 7): the tally carries on from where it stood - the
    /// listeners are wired once in <see cref="Load"/>, and the host raises DamageDealt to them
    /// only while the mod is on.
    /// </summary>
    public void OnEnable() { }

    /// <summary>
    /// Switched off on the main menu: no more blows are counted, and what the tally still has waiting to
    /// be written (a second after the last blow) is written now.
    /// </summary>
    public void OnDisable() => Tally.Flush();

    /// <summary>The window's title, and its toolbar button's.</summary>
    public const string WindowTitle = "DPS Meter";

    /// <summary>Every character's tally (dpsmeter.json in the saves folder).</summary>
    public DpsTally Tally { get; private set; } = new(null);

    public void Load(IModContext ctx)
    {
        void Report(string what, Exception ex) { try { ctx.Log.Error(what, ex); } catch { } }
        Tally = new DpsTally(System.IO.Path.Combine(ctx.DataDirectory, DpsTally.FileName), Report);
        var tally = Tally;
        // the file is written a second after the last blow; what is still waiting when the program
        // closes is written then (the mod system has no unload of its own)
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { tally.Dispose(); } catch { } };
        ctx.Events.DamageDealt += tally.Add;
        ctx.Events.ExperienceGained += tally.Add;
        // a character deleted on the main menu takes their tally with them
        ctx.Events.CharacterDeleted += tally.Remove;
        var settings = ctx.Settings;
        ctx.UI.AddWindow(WindowTitle, () => Draw.View(ctx.Game, tally, Report, settings));
        ctx.Log.Info("ready");
    }

    /// <summary>The way to <see cref="DpsView"/>, typed as object and never inlined (see the class's remarks).</summary>
    private static class Draw
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static object View(ModGame game, DpsTally tally, Action<string, Exception> report, ModSettings settings) =>
            DpsView.Build(game, tally, report, () => settings.GetText(DpsView.ModeKey, "Damage"), m => settings.SetText(DpsView.ModeKey, m));
    }
}
