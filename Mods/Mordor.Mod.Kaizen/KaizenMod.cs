using Mordor.Modding;

namespace Mordor.Mod.Kaizen;

/// <summary>
/// Kaizen: the purely mechanical casting a player would otherwise do by hand, at exactly two
/// moments and never otherwise - no keys, no reacting to anything that happens, no questions.
///
/// - Going down from town (<see cref="ModActions.EnteredDungeonFromTown"/>): every member's own
///   buff list, in party order, once (<see cref="Buffs"/>). The lists are edited in the Kaizen
///   window (<see cref="KaizenView"/>, a toolbar button) and kept in kaizen.json
///   (<see cref="BuffLists"/>).
/// - The stairs up out of level 1 (<see cref="ModActions.LeavingByStairs"/>): heal up before the
///   town's rest (<see cref="HealOut"/>).
///
/// Every cast goes through the dungeon's own cast or Use (<see cref="DungeonContext"/>), so spell
/// points, charges, lines, sounds and refusals are the game's. Neither runs while monsters are
/// present. Both toggles are read as the moment comes, so ticking one takes effect at once.
///
/// The window is reached only through <see cref="Draw"/>, so loading the mod needs no Avalonia:
/// the game has it, the headless probe does not.
/// </summary>
public sealed class KaizenMod : IMod, IModLifecycle
{
    /// <summary>
    /// Switched on again on the main menu (API version 7): nothing to put back - the two moments and the
    /// window are wired once in <see cref="Load"/>, and the host calls them only while the mod is on.
    /// </summary>
    public void OnEnable() { }

    /// <summary>
    /// Switched off on the main menu: the host runs neither moment and closes the Kaizen window. The buff
    /// lists are written as they are edited, so nothing waits to be saved.
    /// </summary>
    public void OnDisable() { }

    public const string BuffOnEntry = "BuffOnEntry";
    public const string HealBeforeTown = "HealBeforeTown";
    /// <summary>The window's title, and its toolbar button's.</summary>
    public const string WindowTitle = "Kaizen";

    /// <summary>Every character's buff list (kaizen.json in the saves folder).</summary>
    public BuffLists Lists { get; private set; } = new(null);

    public void Load(IModContext ctx)
    {
        var s = ctx.Settings;
        void Report(string what, Exception ex) { try { ctx.Log.Error(what, ex); } catch { } }
        Lists = new BuffLists(System.IO.Path.Combine(ctx.DataDirectory, BuffLists.FileName), Report);
        var lists = Lists;
        ctx.Actions.EnteredDungeonFromTown = d =>
            s.Get(BuffOnEntry) ? Buffs.Run(d, lists) : Task.CompletedTask;
        ctx.Actions.LeavingByStairs = d =>
            s.Get(HealBeforeTown) ? HealOut.Run(d) : Task.CompletedTask;
        // a character deleted on the main menu takes their list with them
        ctx.Events.CharacterDeleted += name => lists.Set(name, Array.Empty<BuffRow>());
        ctx.UI.AddWindow(WindowTitle, () => Draw.View(ctx.Game, lists, s, Report));
        ctx.Log.Info("ready");
    }

    /// <summary>The mod's own lines (a skip the game has no words for) say whose they are.</summary>
    public static string Line(string text) => "Kaizen: " + text;

    /// <summary>The way to <see cref="KaizenView"/>, typed as object and never inlined (see the class's remarks).</summary>
    private static class Draw
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static object View(ModGame game, BuffLists lists, ModSettings settings, Action<string, Exception> report) =>
            KaizenView.Build(game, lists, settings, report);
    }
}
