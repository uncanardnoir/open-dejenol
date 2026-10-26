using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mordor.Core;

namespace Mordor.App;

/// <summary>
/// The town's service windows. frmCityMain shows each service modeless - <c>frmCityStore.Show m00B0</c>
/// and the rest, with m00B0 a Static never set, so 0 (FRMCTYM.BAS:183-203) - so the General Store,
/// the Confinement, the Bank, a Guild, the Morgue and the Seer are forms of their own that can be
/// up together, each where it was put, and a picture clicked for one already up only brings it to
/// the front: Show on a loaded form runs no Form_Load. What one does reaches the others that are
/// open: a Bank deposit tells the Store "UPDATE" before anything moves (FRMCITYB.BAS:180), the Bank's
/// Party Pool &amp; Deposit tells the Guild (64), a drag in the pack tells the Store (FRMCHAR3.BAS:583),
/// a switch of character tells all of them (MULTIMOD.BAS:633-637).
///
/// The port had drawn every service in one window, so opening one closed the last and those
/// messages had nobody to reach. Each service now has a window of its own (<see cref="Svc"/>), as
/// the original has a form; the page in it is built as before. <see cref="_svc"/> is the window
/// the code at hand is working in - the one being opened, the one clicked in (a click activates
/// the form it lands on), or the one a message is for - and the names the page builders use
/// (ServicePanel, ServicePage, _serviceWhich, ...) read that window's.
///
/// The Library and the Guild Log, which the Guild opens modally over itself (frmLibrary.Show 1,
/// frmGuildLog.Show 1, FRMCTYG.BAS:170-177), are still drawn in the Guild's window and give it back
/// on closing, as before.
/// </summary>
public partial class CityWindow
{
    /// <summary>One service form's window: the port's page holder, its child window and its state.</summary>
    private sealed class Svc
    {
        public Border Panel = null!;
        public Border Header = null!;
        public TextBlock Title = null!;
        public Button Exit = null!;
        public Border Body = null!;
        public ScrollViewer Scroll = null!;
        public ContentControl Page = null!;
        public MdiChild Child = null!;

        /// <summary>The service this window was opened for: its form. Null for a window the port's own pages opened.</summary>
        public GameWindow? Key;

        /// <summary>The page in it now (see _serviceWhich): the form, or null for the Library or the Guild Log over the Guild.</summary>
        public GameWindow? Which;
        public Avalonia.Size? Form;
        public double ExtraWidth;
        public Action? Return;
        public Func<Avalonia.Input.Key, bool>? Keys;

        public bool Open => Panel.IsVisible;
    }

    /// <summary>Every service window made so far, open or put away for the next one.</summary>
    private readonly List<Svc> _services = new();

    /// <summary>The service window the code at hand is working in (see the class remarks).</summary>
    private Svc _svc = null!;

    // the names the page builders were written against, read off the window at hand
    private Border ServicePanel => _svc.Panel;
    private Border ServiceHeader => _svc.Header;
    private TextBlock ServiceTitle => _svc.Title;
    private Button ServiceExit => _svc.Exit;
    private Border ServiceBody => _svc.Body;
    private ScrollViewer ServiceScroll => _svc.Scroll;
    private ContentControl ServicePage => _svc.Page;
    private MdiChild _serviceChild => _svc.Child;

    /// <summary>
    /// The service up in this window, by its own window-table index - the General Store 11, the
    /// Guilds 12, the Bank 15, the Morgue 16, the Seer 19 and Confinement 5, each form's own constant
    /// (FRMCTYS.BAS:30, FRMCTYG.BAS:33, FRMCITYB.BAS:17, FRMMORG.BAS:27, FRMSEEK.BAS:19,
    /// FRMCITYC.BAS:30) - or the Dungeon for the dead screen, or none for a window of the port's.
    /// </summary>
    private GameWindow? _serviceWhich { get => _svc.Which; set => _svc.Which = value; }

    /// <summary>
    /// The client of the form this window's service is drawn to (<see cref="ShowForm"/>), in pixels;
    /// null for a page of the port's own layout (the dead screen), which keeps the heading strip,
    /// margin and scrolling.
    /// </summary>
    private Avalonia.Size? _serviceForm { get => _svc.Form; set => _svc.Form = value; }

    /// <summary>
    /// How much wider than its own this service's window is made for mods' columns (the Store's,
    /// <see cref="ModSlots.ShowStoreColumns"/>). Nought with none, and for every other service.
    /// </summary>
    private double _serviceExtraWidth { get => _svc.ExtraWidth; set => _svc.ExtraWidth = value; }

    /// <summary>
    /// What closing this window's page goes back to: the Guild, for the Library and the Guild Log,
    /// which the Guild opens modally over itself (<c>frmLibrary.Show 1</c>, <c>frmGuildLog.Show 1</c>,
    /// FRMCTYG.BAS:170-177), so closing them leaves the Guild where it was.
    /// </summary>
    private Action? _serviceReturn { get => _svc.Return; set => _svc.Return = value; }

    /// <summary>The service form's Default and Cancel buttons, pressed for Enter and Escape (the Guild's); null for none.</summary>
    private Func<Avalonia.Input.Key, bool>? _serviceKeys { get => _svc.Keys; set => _svc.Keys = value; }

    /// <summary>The service windows that are up.</summary>
    private IEnumerable<Svc> OpenServices => _services.Where(s => s.Open);

    /// <summary>Whether any service window is up.</summary>
    private bool AnyServiceOpen => _services.Any(s => s.Open);

    /// <summary>The service window that is the active child - the form whose keys these are - or null.</summary>
    private Svc? ActiveService => _services.FirstOrDefault(s => s.Open && Host.Active == s.Child);

    /// <summary>The open service window holding a control, or null.</summary>
    private Svc? ServiceHolding(Avalonia.Visual v) =>
        _services.FirstOrDefault(s => s.Open && Avalonia.VisualTree.VisualExtensions.IsVisualAncestorOf(s.Panel, v));

    /// <summary>The open window standing for a service, or null.</summary>
    private Svc? OpenServiceFor(GameWindow key) => _services.FirstOrDefault(s => s.Open && s.Key == key);

    /// <summary>A new service window: the page holder the port has always used, a child window of its own.</summary>
    private Svc NewServiceWindow()
    {
        var s = new Svc();
        s.Title = new TextBlock { FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#e8e0c0")), IsVisible = false };
        // the port's own heading strip's EXIT: Alt+X while this window is the active one (ServiceAccessKey)
        s.Exit = FormPage.Access(new Button { Focusable = false, Content = new TextBlock { Text = "EXIT" }, Padding = new Avalonia.Thickness(12, 2) }, 'X');
        s.Exit.Click += (_, _) => { _svc = s; CloseService(null, null!); };
        var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(s.Title, 0);
        Grid.SetColumn(s.Exit, 1);
        headerGrid.Children.Add(s.Title);
        headerGrid.Children.Add(s.Exit);
        s.Header = new Border { Background = new SolidColorBrush(Color.Parse("#1c2740")), Padding = new Avalonia.Thickness(8, 5), Child = headerGrid };
        DockPanel.SetDock(s.Header, Dock.Top);
        s.Page = new ContentControl();
        s.Scroll = new ScrollViewer { Content = s.Page };
        s.Body = new Border { Padding = new Avalonia.Thickness(12), Child = s.Scroll };
        var dock = new DockPanel();
        dock.Children.Add(s.Header);
        dock.Children.Add(s.Body);
        s.Panel = new Border
        {
            IsVisible = false,
            BorderBrush = new SolidColorBrush(Color.Parse("#666")),
            BorderThickness = new Avalonia.Thickness(2),
            Background = new SolidColorBrush(Color.Parse("#141420")),
            Child = dock,
        };
        s.Child = _frame.AdoptUnsaved("", s.Panel, ServiceSize.Width, ServiceSize.Height);
        // above the town: a click on the City (a picture for another service) had raised it over a
        // service already up, covering its title bar until it was clicked (2026-10-21)
        s.Child.StaysAbove = _cityChild;
        // the window's own caption carries the title
        s.Title.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty) s.Child.Title = s.Title.Text ?? "";
        };
        // a click in it makes it the form being worked in, as it makes it the active form; on the
        // way down, so the button the click lands on finds it so
        s.Child.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, _) => _svc = s,
                           Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // the Seer's Control-menu Close is its Form_Unload, as its Exit is; before Track, so the
        // placement is still there to save
        s.Child.Closed += _ => { _svc = s; CloseService(null, null!); };
        MdiFrame.Track(s.Panel, s.Child);
        _services.Add(s);
        return s;
    }

    /// <summary>
    /// The window a service opens in: its own if it is up, else one put away, else a new one. A
    /// page of the port's (<paramref name="which"/> null: the Library, the Guild Log) goes in the
    /// window at hand.
    /// </summary>
    private Svc WindowFor(GameWindow? which)
    {
        if (which == null) return _svc;
        if (OpenServiceFor(which.Value) is Svc open) return open;
        var free = _services.FirstOrDefault(s => !s.Open) ?? NewServiceWindow();
        free.Key = which;
        return free;
    }

    /// <summary>
    /// A town picture clicked for a service already up: <c>frmCityStore.Show</c> on a loaded form
    /// (FRMCTYM.BAS:192-203) brings it to the front and makes it the active form, and runs no
    /// Form_Load - what is in it stays as it is. True when that is what happened.
    /// </summary>
    private bool BringUpOpenService(GameWindow which)
    {
        if (OpenServiceFor(which) is not Svc open || open.Which != which) return false;
        _svc = open;
        Host.BringToFront(open.Child);
        Host.Active = open.Child;
        return true;
    }

    /// <summary>
    /// Whether sub0930's screen is owed: the character is dead and alone. sub0930 puts lblDead and
    /// cmdWait up only for a lone character - with anyone else loaded it tells frmParty "UPDATE" and
    /// leaves (MAINMOD2.BAS:3035-3038) - and frmParty's UPDATE unloads a member at nought from a
    /// party (FRMGROUP.BAS:321-346), so the dead screen never stands for a party. imgPicture_Click
    /// asks nothing about death (FRMCTYM.BAS:183-190): with company, a dead member in command opens
    /// the services like anyone.
    /// </summary>
    private bool DeadScreenOwed => _ch.IsDead && _party.Size == 1;

    /// <summary>
    /// The dead screen taken down once it is no longer owed - the character in command alive, by a
    /// raise or a switch, or company come - and frmCityMain shown in its place, as sub14CC unloads
    /// frmDungeonMain and shows the town at level 0 (MAINMOD2.BAS:3150-3152) and mdrmulSwitch's town
    /// branch puts the town forward for a first member alive in town (MULTIMOD.BAS:631-637). Run by
    /// every <see cref="RefreshSummary"/>, which every such change ends in.
    /// </summary>
    private void SyncDeadScreen()
    {
        if (DeadScreenOwed || OpenServiceFor(GameWindow.Dungeon) is not Svc dead) return;
        InServiceWindow(dead, () => CloseService(null, null!));
    }

    /// <summary>
    /// frmParty's "UPDATE" in town, <c>modUpdateCharacterList</c> (FRMGROUP.BAS:310-389): while more than
    /// one is loaded, everyone at nought hits is set dead if not already (an ordinary death), said
    /// "\"X\" has died!" when on the first member's level, and unloaded - mdrcharUnload, which saves
    /// them as they stand (MAINMOD.BAS:419-423) - the last one left never. The town sends it as the
    /// dungeon does: sub0930 for a death in company (MAINMOD2.BAS:3035-3037), a join's and each
    /// autoloaded member's sub154A (MULTIMOD.BAS:382, 266), a leave's sub1776 (755). The dungeon's
    /// is DungeonWindow.PruneTheDead. <paramref name="quiet"/>: the autoload's, said while gv1B52 is
    /// still False, so sub085B drops the lines (MAINMOD2.BAS:3198-3200) - mdrmulLoadParty runs at 6306.
    /// </summary>
    private void PruneTheDead(bool quiet = false)
    {
        if (_party.Size <= 1) return;
        int before = _party.Size;
        var said = _party.RemoveTheDead(
            c => c.Save(System.IO.Path.Combine(SavesDir, Character.SafeFileName(c.Name) + ".json")),
            c =>
            {
                // mdrcharSetDead l0118, 1 (FRMGROUP.BAS:333-335; MAINMOD2.BAS:4558): an ordinary death,
                // counted, written to the guild's log at the floor they are on, DIE.WAV
                c.CurHits = 0; c.IsDead = true; c.Deaths++;
                c.CauseOfDeath = Rules.DeathOrdinary;
                GuildLog.Append(SavesDir, GuildData.IndexOf(c.Guild.Guild),
                                c.LostLevel ? GuildLog.Wrote.DiedSomewhere(c.Name) : GuildLog.Wrote.DiedOnLevel(c.Name, c.DungeonLevel));
                Audio.PlayOnNextChannel("DIE");
            });
        if (_party.Size == before) return;
        if (!quiet) foreach (string line in said) Log(line);
        RefreshSummary();
    }

    /// <summary>Something done in the window standing for a service, whichever window is at hand; nothing when it is not up.</summary>
    private void InServiceWindow(GameWindow which, Action act)
    {
        if (OpenServiceFor(which) is Svc w) InServiceWindow(w, act);
    }

    /// <summary>Something done in a given service window, whichever is at hand.</summary>
    private void InServiceWindow(Svc w, Action act)
    {
        var was = _svc;
        _svc = w;
        try { act(); }
        finally { if (was.Open || !_svc.Open) _svc = was; }
    }
}
