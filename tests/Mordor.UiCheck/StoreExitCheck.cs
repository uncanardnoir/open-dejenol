using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mordor.App;
using Mordor.Core;
using Mordor.Modding;

/// <summary>
/// The General Store left by its EXIT with the keyboard focus in a Buy-list row that has been
/// scrolled out of view. The DataGrid keeps such a row loaded and unloads it when the focus leaves
/// it; taken down with the focus still there, that happened in the middle of the rows' own detach
/// (ArgumentOutOfRangeException in Visual.OnDetachedFromVisualTreeCore, from CloseService). Each
/// step the crash needs is checked on its own, so a run says which of them did not hold.
/// </summary>
static class StoreExitCheck
{
    public static int Run(string game)
    {
        Console.WriteLine("\nGeneral Store: EXIT with a clicked row scrolled out of view:");
        int failed = 0;
        void Check(string what, bool ok)
        {
            Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {what}");
            if (!ok) failed++;
        }
        void Pump()
        {
            for (int i = 0; i < 4; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        }
        string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-storeexit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string? savesWas = Environment.GetEnvironmentVariable(Saves.EnvDir);
        var hostWas = ModHost.Active;
        CityWindow? town = null;
        try
        {
            Environment.SetEnvironmentVariable(Saves.EnvDir, scratch);
            ModHost.Active = new ModHost(new ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
            Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
            var hero = new Character
            {
                Name = "Exitcheck", Race = RaceData.All[0].Name, CurHits = 100, MaxHits = 100, GoldOnHand = 100_000,
                Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 5, Experience = 10_000 } },
            };
            hero.Save(Path.Combine(scratch, Character.SafeFileName(hero.Name) + ".json"));
            town = new CityWindow(new Party(hero), game) { Width = 1400, Height = 1000 };
            town.Show(); Pump();
            typeof(CityWindow).GetMethod("ShowStore", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(town, new object?[] { null, new RoutedEventArgs() });
            Pump();
            var w = town;
            var grid = w.GetVisualDescendants().OfType<DataGrid>().First();
            string? Caption(Button b) => (b.Content as TextBlock)?.Text;
            var exit = w.GetVisualDescendants().OfType<Button>()
                .First(b => Caption(b) == "EXIT" && b.Parent is Visual p && p.IsVisualAncestorOf(grid));
            Point Mid(Visual v) => v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), w)!.Value;
            void Click(Point at) { w.MouseDown(at, MouseButton.Left); Pump(); w.MouseUp(at, MouseButton.Left); Pump(); }
            List<DataGridRow> Shown() => grid.GetVisualDescendants().OfType<DataGridRow>()
                .Where(r => r.IsVisible && r.Index >= 0 && r.Bounds.Height > 0).OrderBy(r => r.Index).ToList();
            string Focused() => w.FocusManager?.GetFocusedElement()?.GetType().Name ?? "nothing";

            int total = (grid.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().Count() ?? 0;
            var shown = Shown();
            Console.WriteLine($"        the list has {total} rows, {shown.Count} loaded; the focus is on {Focused()}");
            var row = shown[Math.Min(1, shown.Count - 1)];
            int clicked = row.Index;
            Click(row.TranslatePoint(new Point(20, row.Bounds.Height / 2), w)!.Value);
            // more than one alignment in stock: Select Alignment holds the list and EXIT until one is pressed
            var align = w.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => Caption(b) is "U" or "G" or "N" or "E" && b.IsEffectivelyVisible && b.IsEffectivelyEnabled
                                     && b.GetVisualAncestors().Any(a => a == exit.Parent));
            if (align != null) { Console.WriteLine($"        Select Alignment came up; pressing {Caption(align)}"); Click(Mid(align)); }
            Check($"a click on row {clicked} leaves the keyboard focus in the list (it is on {Focused()})",
                w.FocusManager?.GetFocusedElement() is Visual f && grid.IsVisualAncestorOf(f));

            var over = Mid(grid);
            for (int i = 0; i < 80 && Shown().Where(r => r != row).Select(r => r.Index).DefaultIfEmpty(0).Min() <= clicked + 3; i++)
            {
                w.MouseWheel(over, new Vector(0, -1)); Pump();
            }
            int top = Shown().Where(r => r != row).Select(r => r.Index).DefaultIfEmpty(0).Min();
            Check($"the wheel takes row {clicked} out of view (the top row is now {top} of {total})", top > clicked + 3);
            Check($"the focus is still in the list after the wheel (it is on {Focused()})",
                w.FocusManager?.GetFocusedElement() is Visual g && grid.IsVisualAncestorOf(g));
            Check("EXIT answers", exit.IsEffectivelyEnabled && exit.IsEffectivelyVisible);

            Exception? thrown = null;
            try { Click(Mid(exit)); }
            catch (Exception ex) { thrown = ex; }
            Check("EXIT takes the Store down without an exception" + (thrown == null ? "" : $": {thrown.GetType().Name}: {thrown.Message}"), thrown == null);
            Check("the Store's page is gone", !((Visual)grid).IsAttachedToVisualTree());
        }
        catch (Exception ex) { Check("the Store can be driven: " + ex, false); }
        finally
        {
            try { town?.Close(); Pump(); } catch { }
            ModHost.Active = hostWas;
            Environment.SetEnvironmentVariable(Saves.EnvDir, savesWas);
            if (new DirectoryInfo(scratch).Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
                throw new InvalidOperationException("Refusing cleanup outside the store-exit test folder.");
            Directory.Delete(scratch, true);
        }
        return failed;
    }
}
