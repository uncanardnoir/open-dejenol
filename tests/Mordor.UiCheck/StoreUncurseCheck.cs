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

static class StoreUncurseCheck
{
    public static int Run(string game)
    {
        Console.WriteLine("\nGeneral Store: uncursing through the real input, drop and payment controls:");
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
        string scratch = Path.Combine(Path.GetTempPath(), "mordor-uicheck-uncurse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string? savesWas = Environment.GetEnvironmentVariable(Saves.EnvDir);
        var hostWas = ModHost.Active;
        CityWindow? town = null;
        try
        {
            Environment.SetEnvironmentVariable(Saves.EnvDir, scratch);
            ModHost.Active = new ModHost(new ModSettingsStore(Path.Combine(scratch, "settings.ini")), _ => { });
            Mdata1.LoadFrom(game); RaceData.LoadFrom(game); GuildData.LoadFrom(game);
            var items = MdrLoader.LoadItems(Path.Combine(game, "MDATA3.MDR"));
            var ball = items.First(i => i.Name == "Ball and Chain");
            var cursed = items.First(i => i.Cursed == 1 && !ItemUse.IsGuildIssued(i));
            var ordinary = items.First(i => i.Id > 0 && i.Cursed == 0 && !ItemUse.IsGuildIssued(i));
            var hero = new Character
            {
                Name = "Uncursecheck", Race = RaceData.All[0].Name, CurHits = 100, MaxHits = 100, GoldOnHand = 100_000_000,
                Guilds = { new GuildMembership { Guild = GuildData.All[0].Name, Level = 20, Experience = 2_000_000 } },
            };
            hero.Pack[1] = new InventorySlot { ItemId = ball.Id, Equipped = true, SlotCursed = true, Known = IdLevel.Barely };
            hero.Pack[2] = new InventorySlot { ItemId = cursed.Id, Equipped = true, SlotCursed = true, Known = IdLevel.All };
            hero.Pack[3] = new InventorySlot { ItemId = ordinary.Id, Known = IdLevel.All };
            hero.Save(Path.Combine(scratch, Character.SafeFileName(hero.Name) + ".json"));
            town = new CityWindow(new Party(hero), game) { Width = 1400, Height = 1000 };
            town.Show(); Pump();
            typeof(CityWindow).GetMethod("ShowStore", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(town, new object?[] { null, new RoutedEventArgs() });
            Pump();
            var button = town.GetVisualDescendants().OfType<Button>().Single(b => b.Content is TextBlock { Text: "Uncurse" });
            var panel = (Canvas)button.Parent!;
            var input = panel.Children.OfType<TextBox>().Single();
            var cost = panel.Children.OfType<Border>().Select(b => b.Child).OfType<TextBlock>().Single();
            string Fee(Item item) => ((long)Math.Round(0.8 * item.Price / 0.5, MidpointRounding.ToEven)).ToString("N0");
            void Enter() { input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); Pump(); }
            void Quote(string text) { input.Text = text; Pump(); Enter(); }
            Point Mid(Control control) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), town)!.Value;
            void Drop(int slot)
            {
                var data = (DragPayload.Item(slot) with { Owner = hero }).Wrapped();
                var point = Mid(input);
                town.DragDrop(point, Avalonia.Input.Raw.RawDragEventType.DragEnter, data, DragDropEffects.Move, RawInputModifiers.None);
                town.DragDrop(point, Avalonia.Input.Raw.RawDragEventType.DragOver, data, DragDropEffects.Move, RawInputModifiers.None);
                town.DragDrop(point, Avalonia.Input.Raw.RawDragEventType.Drop, data, DragDropEffects.Move, RawInputModifiers.None);
                Pump();
            }
            void Uncurse(int index, Item item)
            {
                var before = hero.GoldOnHand;
                long fee = (long)Math.Round(0.8 * item.Price / 0.5, MidpointRounding.ToEven);
                var point = Mid(button);
                town.MouseDown(point, MouseButton.Left); town.MouseUp(point, MouseButton.Left); Pump();
                Check($"uncurse {item.Name}: one click separates and fully identifies the item",
                    hero.Pack[index] is { Equipped: false, SlotCursed: false, Known: IdLevel.All });
                Check($"uncurse {item.Name}: the quoted fee is charged exactly once", hero.GoldOnHand == before - fee);
                Check("successful uncurse clears its input, quote and button", input.Text == "" && cost.Text == "" && !button.IsEnabled);
                var saved = Character.Load(Path.Combine(scratch, Character.SafeFileName(hero.Name) + ".json"));
                Check("the repaired character and experience survive save/reload", saved.Pack[index] is { Equipped: false, SlotCursed: false }
                    && saved.GoldOnHand == hero.GoldOnHand && saved.Guilds[0].Experience == 2_000_000);
            }
            Quote("2");
            Check("one Enter retains the Ball and Chain quote after queued events drain", button.IsEnabled && cost.Text == Fee(ball));
            Enter(); // also exercises the existing second-Enter workaround
            Check("reconfirming a quoted caption retains its fee", button.IsEnabled && cost.Text == Fee(ball));
            input.Text = "3";
            Check("editing a quoted slot clears the stale offer", !button.IsEnabled && cost.Text == "");
            Pump();
            Quote("2");
            Uncurse(1, ball);
            Drop(2);
            Check("one drop retains the known cursed item's quote after queued events drain", button.IsEnabled && cost.Text == Fee(cursed));
            Uncurse(2, cursed);
            Quote("4");
            var purse = hero.GoldOnHand;
            Check("an ordinary item still gets a quote before the original curse refusal", button.IsEnabled && cost.Text == Fee(ordinary));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check("the not-cursed refusal does not charge or equip the item", hero.GoldOnHand == purse && !hero.Pack[3]!.Equipped);
            foreach (string invalid in new[] { "", "1", "40", "999" })
            {
                Quote(invalid);
                Check($"invalid or empty slot '{invalid}' has no actionable quote", !button.IsEnabled && cost.Text == "");
            }
        }
        catch (Exception ex) { Check("uncurse UI can be driven: " + ex, false); }
        finally
        {
            town?.Close(); Pump(); ModHost.Active = hostWas;
            Environment.SetEnvironmentVariable(Saves.EnvDir, savesWas);
            if (new DirectoryInfo(scratch).Parent?.FullName != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())))
                throw new InvalidOperationException("Refusing cleanup outside the uncurse test folder.");
            Directory.Delete(scratch, true);
        }
        return failed;
    }
}
