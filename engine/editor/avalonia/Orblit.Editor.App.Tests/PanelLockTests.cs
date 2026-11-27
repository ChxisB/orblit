using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class PanelLockTests
{
    const string Refusal = "The layout is locked. Unlock it in View or a panel's menu.";

    static Point Middle(Control control, EditorWindow window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    static void Drag(Point from, Point to, EditorWindow window)
    {
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
        window.UpdateLayout();
    }

    static void Lock(EditorWindow window)
    {
        Click(window.SceneTabs.LockButton);
        window.UpdateLayout();
    }

    static void Press(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static MenuItem ItemOf(DockTabs group, string header) =>
        ((MenuFlyout)group.MenuButton.Flyout!).Items.OfType<MenuItem>().Single(item => AutomationProperties.GetName(item) == header);

    static string Words(MenuItem item) =>
        string.Join(" ", ((Control)item.Header!).GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text));

    [Fact]
    public void The_button_locks_the_layout_and_unlocks_it_again()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var button = window.SceneTabs.LockButton;
            Assert.Equal("Lock layout", AutomationProperties.GetName(button));

            Lock(window);
            Assert.True(window.Docks.Locked);
            Assert.Equal("Unlock layout", AutomationProperties.GetName(button));
            Assert.Equal("Locked the layout. Panels stay where they are.", window.Status);

            Lock(window);
            Assert.False(window.Docks.Locked);
            Assert.Equal("Lock layout", AutomationProperties.GetName(button));
            Assert.Equal("Unlocked the layout. Panels can move again.", window.Status);
        });
    }

    [Fact]
    public void A_locked_layout_turns_off_each_panel_menu_move_and_says_why()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var dock = window.InspectorDock;

            Lock(window);

            Assert.All(dock.MoveItems, item => Assert.False(item.IsEnabled));
            Assert.False(dock.FloatItem.IsEnabled);
            Assert.False(ItemOf(dock, "Put all panels back").IsEnabled);
            Assert.Contains("The layout is locked", Words(dock.FloatItem));
            Assert.Contains("The layout is locked", Words(dock.MoveItems[(int)DockSlot.UpperLeft]));
            Assert.True(ItemOf(dock, "Lock layout").IsChecked);

            Press(ItemOf(dock, "Lock layout"));
            Assert.False(window.Docks.Locked);
            Assert.True(dock.FloatItem.IsEnabled);
            Assert.True(ItemOf(dock, "Put all panels back").IsEnabled);
            Assert.False(ItemOf(dock, "Lock layout").IsChecked);
        });
    }

    [Fact]
    public void A_drag_on_a_locked_layout_moves_nothing_and_says_where_to_unlock()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Lock(window);
            var signals = Middle(window.InspectorDock.Tabs[1], window);

            window.MouseDown(signals, MouseButton.Left);
            window.MouseMove(Middle(window.FileDock, window), RawInputModifiers.LeftMouseButton);

            Assert.Equal(Refusal, window.Status);
            Assert.Empty(AdornerLayer.GetAdornerLayer(window.FileDock)!.Children.OfType<DropMark>());

            window.MouseUp(Middle(window.FileDock, window), MouseButton.Left);
            Assert.Equal(["Inspector", "Signals"], window.InspectorDock.Titles);
            Assert.Equal(["Files", "History"], window.FileDock.Titles);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_splitters_move_only_while_the_layout_is_unlocked(bool locked)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var scene = BoxOf(window.SceneDock, window);
            var files = BoxOf(window.FileDock, window);
            if (locked)
            {
                Lock(window);
            }

            var column = Middle(window.LeftSide.Splitter, window);
            Drag(column, column + new Point(80, 0), window);
            var row = new Point(scene.X + 40, scene.Y + scene.Height + 4);
            Drag(row, row + new Point(0, 80), window);

            Assert.Equal(locked, scene == BoxOf(window.SceneDock, window));
            Assert.Equal(locked, files == BoxOf(window.FileDock, window));
        });
    }

    [Fact]
    public void A_floating_panel_stops_resizing_while_locked_and_still_closes_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Press(window.FileDock.FloatItem);
            var floating = Assert.IsType<Window>(TopLevel.GetTopLevel(window.FileDock));

            Lock(window);
            Assert.False(floating.CanResize);
            // Put it back does what closing the window does.
            Assert.True(window.FileDock.FloatItem.IsEnabled);

            Lock(window);
            Assert.True(floating.CanResize);

            Lock(window);
            floating.Close();
            window.UpdateLayout();
            Assert.Same(window, TopLevel.GetTopLevel(window.FileDock));
        });
    }

    [Fact]
    public void A_locked_layout_still_collapses_and_opens_panels()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Lock(window);

            Click(window.FileDock.CollapseButton);
            window.UpdateLayout();
            Assert.True(window.LeftSide.Rail.IsVisible);

            Click(window.LeftSide.RailButtons.Single());
            window.UpdateLayout();
            Assert.False(window.LeftSide.Rail.IsVisible);
        });
    }

    [Fact]
    public void The_View_menu_locks_the_layout_and_ticks_its_item()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var item = NativeMenu.GetMenu(window)!.Items.OfType<NativeMenuItem>().Single(top => top.Header == "View")
                .Menu!.Items.OfType<NativeMenuItem>().Single(each => each.Header == "Lock layout");

            item.Command!.Execute(item.CommandParameter);

            Assert.True(window.Docks.Locked);
            Assert.True(item.IsChecked);
        });
    }
}
