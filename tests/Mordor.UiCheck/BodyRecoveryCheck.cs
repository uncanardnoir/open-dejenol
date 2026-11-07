using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mordor.App;
using Mordor.Core;

static class BodyRecoveryCheck
{
    public static int Run(string game)
    {
        Console.WriteLine("\nrecovering a character who dies on a step:");
        int failed = 0;
        void Check(string name, bool ok)
        {
            Console.WriteLine($"  {(ok ? "ok  " : "FAIL")}  {name}");
            if (!ok) failed++;
        }
        void Pump()
        {
            for (int i = 0; i < 6; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }
        Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
        foreach (bool differentRoom in new[] { false, true })
        {
            string scenario = differentRoom ? "crossing into another room" : "walking within a room";
            string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-body-" + Guid.NewGuid().ToString("N"));
            string? previousSaves = Environment.GetEnvironmentVariable(Saves.EnvDir);
            Environment.SetEnvironmentVariable(Saves.EnvDir, scratch);
            DungeonWindow? window = null;
            try
            {
                // Synthetic adjacent squares, not a location or change in the player's dungeon.
                var floor = new Floor { Width = 5, Height = 5, LevelNum = 1, Fields = new Floor.Field[25] };
                floor.Areas.AddRange(Enumerable.Repeat(new Floor.Area { LairId = -1 }, 3));
                floor.Fields[1 * 5 + 1].AreaId = 1;
                floor.Fields[2 * 5 + 1] = new Floor.Field { AreaId = (short)(differentRoom ? 2 : 1), Flags = 1L << Floor.PIT };
                Character Make(string name, int hits) => new()
                {
                    Name = name, Race = RaceData.All[0].Name, CurHits = hits, MaxHits = hits,
                    DungeonLevel = 1, X = 1, Y = 1, Area = 1, Facing = 1,
                    Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 1 } },
                };
                var rescuer = Make("Body rescuer", 10000);
                var fallen = Make("Body fallen", 1); // a pit always deals at least one hit
                // Exercise pruning both the leader and a second member.
                var party = differentRoom ? new Party(rescuer, fallen) : new Party(fallen, rescuer);
                party.SaveAll(scratch);
                window = new DungeonWindow(party, new List<Floor> { floor }, game, 1, 1, 1);
                window.Show(); Pump();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (string name in new[] { "_clock", "_ailmentClock" })
                    ((DispatcherTimer?)typeof(DungeonWindow).GetField(name, flags)!.GetValue(window))?.Stop();
                // Death/unload must save the body even with ordinary autosaves disabled.
                object options = typeof(DungeonWindow).GetField("_options", flags)!.GetValue(window)!;
                options.GetType().GetProperty("NoAutoSave")!.SetValue(options, true);
                window.Focus();
                window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, "");
                window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, ""); Pump();
                Check(scenario + ": the fall killed and unloaded the character", fallen.IsDead && party.Size == 1 && party.Leader == rescuer);
                var saved = Character.Load(Path.Combine(scratch, Character.SafeFileName(fallen.Name) + ".json"));
                Check(scenario + ": body saved on the fatal square and room",
                    saved.IsDead && saved.DungeonLevel == 1 && saved.X == 1 && saved.Y == 2 && saved.Area == (differentRoom ? 2 : 1));
                window.KeyPress(Key.P, RawInputModifiers.None, PhysicalKey.None, "p");
                window.KeyRelease(Key.P, RawInputModifiers.None, PhysicalKey.None, "p"); Pump();
                var picker = window.OwnedWindows.FirstOrDefault(w => w.Title == "Pickup Character");
                var nameBox = picker?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
                bool offered = nameBox is { IsEnabled: true } && nameBox.Text == fallen.Name;
                Check(scenario + ": Pickup offers the fallen character", offered);
                if (offered)
                {
                    picker!.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Pump();
                }
                else picker?.Close();
                Check(scenario + ": the survivor can carry the body", rescuer.CarryingCharacter == fallen.Name);
            }
            catch (Exception ex)
            {
                Check(scenario + ": could be driven (" + ex + ")", false);
            }
            finally
            {
                if (window != null)
                {
                    foreach (var child in window.OwnedWindows.ToArray()) child.Close();
                    window.Close(); Pump();
                }
                Environment.SetEnvironmentVariable(Saves.EnvDir, previousSaves);
                if (new DirectoryInfo(scratch).Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
                    || !Path.GetFileName(scratch).StartsWith("mordor-uicheck-body-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing cleanup outside the body test's temporary folder.");
                Directory.Delete(scratch, true);
            }
        }
        return failed;
    }
}
