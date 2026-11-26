using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class PanelDragTests
{
    static Point At(Control control, double x, double y, EditorWindow window) =>
        control.TranslatePoint(new Point(control.Bounds.Width * x, control.Bounds.Height * y), window)!.Value;

    static Point Middle(Control control, EditorWindow window) => At(control, 0.5, 0.5, window);

    // The strip is the top of a group.
    static Point Strip(DockTabs group, EditorWindow window) => group.TranslatePoint(new Point(group.Bounds.Width / 2, DockTabs.StripHeight / 2), window)!.Value;

    static DockTabs GroupOf(string title, EditorWindow window) =>
        window.GetVisualDescendants().OfType<DockTabs>().Single(group => group.Titles.Contains(title));

    static Border TabOf(string title, EditorWindow window)
    {
        var group = GroupOf(title, window);
        return group.Tabs[group.Titles.ToList().IndexOf(title)];
    }

    // A move with the button held, as a real drag reports it.
    static void Move(Point to, EditorWindow window) => window.MouseMove(to, RawInputModifiers.LeftMouseButton);

    // A press and a move past the slack, which starts a drag of the tab.
    static void Pick(string title, EditorWindow window)
    {
        var from = Middle(TabOf(title, window), window);
        window.MouseDown(from, MouseButton.Left);
        Move(from + new Point(0, 20), window);
    }

    static void Drop(Point to, EditorWindow window)
    {
        Move(to, window);
        window.MouseUp(to, MouseButton.Left);
        window.UpdateLayout();
    }

    static void Drag(string title, Point to, EditorWindow window)
    {
        Pick(title, window);
        Drop(to, window);
    }

    static string[] Marks(Control on) =>
        [.. AdornerLayer.GetAdornerLayer(on)!.Children.OfType<DropMark>().Select(mark => mark.Words)];

    static void Press(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static MenuItem ItemOf(DockTabs group, string header) =>
        ((MenuFlyout)group.MenuButton.Flyout!).Items.OfType<MenuItem>().Single(item => AutomationProperties.GetName(item) == header);

    [Fact]
    public void A_press_that_barely_moves_picks_the_tab_and_moves_nothing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var from = Middle(window.InspectorDock.Tabs[1], window);

            window.MouseDown(from, MouseButton.Left);
            Move(from + new Point(2, 2), window);
            window.MouseUp(from + new Point(2, 2), MouseButton.Left);

            Assert.Equal(1, window.InspectorDock.SelectedIndex);
            Assert.Equal(["Inspector", "Signals"], window.InspectorDock.Titles);
            Assert.Empty(Marks(window.InspectorDock));
        });
    }

    [Fact]
    public void A_tab_dropped_on_a_strip_joins_that_group()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Drag("Signals", Strip(window.FileDock, window), window);

            Assert.Equal(["Files", "History", "Signals"], window.FileDock.Titles);
            Assert.Equal(["Inspector"], window.InspectorDock.Titles);
            Assert.Equal("Signals", window.FileDock.Chosen.Title);
            Assert.Contains(window.FileDock.Content, window.FileDock.GetVisualDescendants());
            Assert.Equal("Moved Signals in with Files.", window.Status);
        });
    }

    [Fact]
    public void A_mark_says_where_the_drop_goes_and_takes_no_clicks()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Pick("Signals", window);
            Move(Strip(window.FileDock, window), window);
            window.UpdateLayout();

            var mark = Assert.Single(AdornerLayer.GetAdornerLayer(window.FileDock)!.Children.OfType<DropMark>());
            Assert.Equal("Add to Files", mark.Words);
            Assert.False(mark.IsHitTestVisible);

            Drop(Strip(window.FileDock, window), window);
            Assert.Empty(Marks(window.FileDock));
        });
    }

    [Fact]
    public void A_group_whose_last_tab_leaves_goes_and_gives_its_room()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var files = window.FileDock;

            Drag("History", Strip(window.InspectorDock, window), window);
            Drag("Files", Strip(window.InspectorDock, window), window);

            Assert.Null(TopLevel.GetTopLevel(files));
            AssertBox(8, 46, 280, 1225, BoxOf(window.SceneDock, window));
            Assert.Equal(["Inspector", "Signals", "History", "Files"], window.InspectorDock.Titles);
        });
    }

    [Fact]
    public void The_bottom_third_of_a_group_that_fills_its_side_splits_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Drag("Signals", At(window.InspectorDock, 0.5, 0.9, window), window);

            var signals = GroupOf("Signals", window);
            Assert.NotSame(window.InspectorDock, signals);
            Assert.Equal(DockSlot.UpperRight, window.InspectorDock.Slot);
            Assert.Equal(DockSlot.LowerRight, signals.Slot);
            Assert.True(BoxOf(signals, window).Y > BoxOf(window.InspectorDock, window).Y);
            Assert.Equal("Moved Signals to the right column, bottom.", window.Status);
        });
    }

    [Fact]
    public void The_top_third_of_its_own_group_splits_the_tab_off_above()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var signals = window.InspectorDock;

            Drag("Signals", At(signals, 0.5, 0.15, window), window);

            Assert.Equal(DockSlot.UpperRight, GroupOf("Signals", window).Slot);
            Assert.Equal(DockSlot.LowerRight, GroupOf("Inspector", window).Slot);
        });
    }

    [Fact]
    public void A_drop_on_the_other_group_of_a_full_side_joins_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            // The side holds two groups, so there is no room to split.
            Drag("Signals", At(window.FileDock, 0.5, 0.9, window), window);

            Assert.Same(window.FileDock, GroupOf("Signals", window));
        });
    }

    [Fact]
    public void A_drop_at_the_edge_of_an_empty_side_docks_there()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Drag("Inspector", Strip(window.FileDock, window), window);
            Drag("Signals", Strip(window.FileDock, window), window);
            Assert.False(window.RightSide.IsVisible);

            Drag("Files", new Point(window.Bounds.Width - 20, 600), window);

            var files = GroupOf("Files", window);
            Assert.Equal(DockSlot.UpperRight, files.Slot);
            Assert.True(files.IsEffectivelyVisible);
            Assert.Equal(["Files"], files.Titles);
        });
    }

    [Fact]
    public void A_drop_on_a_rail_button_joins_that_group_and_opens_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Click(window.FileDock.CollapseButton);
            window.UpdateLayout();

            Drag("Signals", Middle(window.LeftSide.RailButtons.Single(), window), window);

            Assert.Same(window.FileDock, GroupOf("Signals", window));
            Assert.True(window.FileDock.IsEffectivelyVisible);
            Assert.False(window.LeftSide.Rail.IsVisible);
        });
    }

    [Fact]
    public void A_drop_in_the_middle_floats_the_tab_and_closing_the_window_puts_it_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Drag("History", Middle(window.Center, window), window);

            var history = window.Docks.GroupOf("History");
            var floating = Assert.IsType<Window>(TopLevel.GetTopLevel(history));
            Assert.NotSame(window, floating);
            Assert.Equal("History", floating.Title);
            Assert.Equal(["Files"], window.FileDock.Titles);
            Assert.Equal("History is in a window of its own. Close it, or choose Put it back in its menu.", window.Status);

            floating.Close();
            window.UpdateLayout();

            Assert.Equal(["Files", "History"], window.FileDock.Titles);
            Assert.Null(TopLevel.GetTopLevel(history));
        });
    }

    [Fact]
    public void A_floating_panel_whose_place_is_taken_joins_the_group_it_is_moved_to()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Drag("History", Middle(window.Center, window), window);

            // Files still sits where History came from, so there is no place to trade.
            Press(window.Docks.GroupOf("History").MoveItems[(int)DockSlot.UpperRight]);
            window.UpdateLayout();

            Assert.Equal(["Inspector", "Signals", "History"], window.InspectorDock.Titles);
            Assert.Equal("History", window.InspectorDock.Chosen.Title);
            Assert.Equal("Moved History in with Inspector.", window.Status);
        });
    }

    [Fact]
    public void A_tab_dropped_on_a_floating_window_joins_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var spot = Middle(window.SceneDock, window);
            Press(window.SceneDock.FloatItem);
            window.UpdateLayout();

            Drag("Signals", spot, window);

            Assert.Equal(["Scene", "Import", "Signals"], window.SceneDock.Titles);
            Assert.NotSame(window, TopLevel.GetTopLevel(window.SceneDock));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Escape_or_a_lost_pointer_cancels_the_drag(bool escape)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            IPointer? pointer = null;
            window.AddHandler(InputElement.PointerMovedEvent, (_, e) => pointer = e.Pointer, handledEventsToo: true);
            var to = Strip(window.FileDock, window);

            Pick("Signals", window);
            Move(to, window);
            if (escape)
            {
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            }
            else
            {
                pointer!.Capture(null);
            }

            window.UpdateLayout();
            Assert.Empty(Marks(window.FileDock));

            Drop(to, window);
            Assert.Equal(["Inspector", "Signals"], window.InspectorDock.Titles);
            Assert.Equal(["Files", "History"], window.FileDock.Titles);
        });
    }

    [Fact]
    public void A_dropped_tab_takes_its_menu_item_and_its_ways_in_with_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Drag("Files", Strip(window.InspectorDock, window), window);
            window.InspectorDock.Select(0);

            window.Docks.Show("Files");
            Assert.Equal("Files", window.InspectorDock.Chosen.Title);

            Assert.DoesNotContain(((MenuFlyout)window.FileDock.MenuButton.Flyout!).Items.OfType<MenuItem>(), item => AutomationProperties.GetName(item) == "Files");
            Press(ItemOf(window.InspectorDock, "Files"));
            Assert.Equal("Inspector", window.InspectorDock.Chosen.Title);
            Assert.False(window.InspectorDock.IsShown("Files"));
        });
    }

    [Fact]
    public void Reset_layout_gives_every_group_its_own_tabs_again()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var files = BoxOf(window.FileDock, window);
            Drag("History", Strip(window.InspectorDock, window), window);
            Drag("Files", At(window.SceneDock, 0.5, 0.5, window), window);
            Drag("Signals", Middle(window.Center, window), window);

            Assert.Equal(["Scene", "Import", "Files"], window.SceneDock.Titles);
            Assert.Equal("Signals", window.Docks.GroupOf("Signals").Title);

            Press(ItemOf(window.SceneDock, "Put all panels back"));
            window.UpdateLayout();

            Assert.Equal(["Scene", "Import"], window.SceneDock.Titles);
            Assert.Equal(["Files", "History"], window.FileDock.Titles);
            Assert.Equal(["Inspector", "Signals"], window.InspectorDock.Titles);
            Assert.Equal(files, BoxOf(window.FileDock, window));
            Assert.Equal(3, window.GetVisualDescendants().OfType<DockTabs>().Count());
        });
    }
}
