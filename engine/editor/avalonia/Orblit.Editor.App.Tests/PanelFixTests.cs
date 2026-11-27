using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Orblit.Editor.App.Launcher;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

/// <summary>Cases where a hidden tab, a floating window or a reset could leave panels lost or wrongly named.</summary>
public sealed class PanelFixTests
{
    static void Press(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static Window Float(DockTabs group, EditorWindow window)
    {
        Click(group.Map.Boxes[^1]);
        window.UpdateLayout();
        return Assert.IsType<Window>(TopLevel.GetTopLevel(group));
    }

    static void Settle(EditorWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [Fact]
    public void Showing_a_hidden_panel_turns_its_tab_on_and_loses_no_other()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Docks.ToggleTab("Files");

            window.Docks.Show("Files");
            Settle(window);

            Assert.True(window.FileDock.IsShown("Files"));
            Assert.Equal("Files", window.FileDock.Chosen.Title);
            Assert.All(window.FileDock.Tabs, tab => Assert.DoesNotContain(OverflowBar.Moved, tab.Classes));
        });
    }

    [Fact]
    public void A_floating_window_whose_tabs_are_all_hidden_closes()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Docks.Drop(window.FileDock.Tabs[1], new DropTarget.Float(window, default), default);
            var floating = Assert.IsType<Window>(TopLevel.GetTopLevel(window.Docks.GroupOf("History")));

            window.Docks.ToggleTab("History");
            Settle(window);

            Assert.False(floating.IsVisible);
            Assert.Same(window.FileDock, window.Docks.GroupOf("History"));
            Assert.False(window.FileDock.IsShown("History"));
        });
    }

    [Fact]
    public void A_floating_window_left_with_only_hidden_tabs_closes()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var floating = Float(window.SceneDock, window);
            window.Docks.ToggleTab("Import");

            window.Docks.Drop(window.SceneDock.Tabs[0], new DropTarget.Join(window.FileDock, window.FileDock, default), default);
            Settle(window);

            Assert.False(floating.IsVisible);
            Assert.Same(window, TopLevel.GetTopLevel(window.Docks.GroupOf("Import")));
        });
    }

    [Fact]
    public void A_group_takes_its_name_from_its_first_tab_that_shows()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var floating = Float(window.SceneDock, window);

            window.Docks.ToggleTab("Scene");
            Settle(window);

            Assert.Equal("Import", window.SceneDock.Title);
            Assert.Equal("Import", floating.Title);
        });
    }

    [Fact]
    public void Putting_all_panels_back_gives_the_sides_their_first_sizes()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var splitter = window.LeftSide.Splitter;
            var from = splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, 200), window)!.Value;
            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(from + new Point(120, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(from + new Point(120, 0), MouseButton.Left);
            window.LeftSide.Split = 0.8;
            window.UpdateLayout();
            Assert.Equal(400, window.LeftSide.Bounds.Width);

            Press(((MenuFlyout)window.SceneDock.MenuButton.Flyout!).Items.OfType<MenuItem>().Single(item => AutomationProperties.GetName(item) == "Put all panels back"));
            Settle(window);

            Assert.Equal(280, window.LeftSide.Bounds.Width);
            Assert.Equal(0.5, window.LeftSide.Split);
        });
    }

    [Fact]
    public void The_close_key_in_a_floating_window_closes_it_and_no_scene()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var floating = Float(window.InspectorDock, window);
            var closed = false;
            window.SceneTabs.CloseButton.Click += (_, _) => closed = true;

            floating.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = floating, Key = Key.W, KeyModifiers = window.CommandKey });
            Settle(window);

            Assert.False(closed);
            Assert.False(floating.IsVisible);
            Assert.Same(window, TopLevel.GetTopLevel(window.InspectorDock));
        });
    }

    [Fact]
    public void A_saved_window_with_no_tab_to_show_does_not_come_back_blank()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"orblit_blank_{Guid.NewGuid():N}");
        try
        {
            var settings = new EditorSettings(Path.Combine(folder, EditorSettings.FileName));
            settings.SetSection("layout", [new("float_1", "History|LowerLeft|100 100 300 400"), new("hidden", "History")]);
            UiThread.Run(() =>
            {
                var window = new EditorWindow(settings: settings) { Width = 1920, Height = 1080 };
                window.Show();
                window.Load(new FakeScene());
                Settle(window);

                Assert.Same(window, TopLevel.GetTopLevel(window.Docks.GroupOf("History")));
            });
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
