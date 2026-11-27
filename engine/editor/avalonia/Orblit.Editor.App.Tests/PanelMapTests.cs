using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class PanelMapTests
{
    const int UpperLeft = (int)DockSlot.UpperLeft;
    const int LowerLeft = (int)DockSlot.LowerLeft;
    const int UpperRight = (int)DockSlot.UpperRight;
    const int LowerRight = (int)DockSlot.LowerRight;
    const int Floating = 4;

    static MenuFlyout MenuOf(DockTabs group) => (MenuFlyout)group.MenuButton.Flyout!;

    // The menu opens with a real press, in an overlay of the window the group is in.
    static void OpenMenu(DockTabs group)
    {
        var window = (Window)TopLevel.GetTopLevel(group)!;
        var button = group.MenuButton;
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    static Point CenterOf(Control control, Visual top) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), top)!.Value;

    static void PressBox(DockTabs group, int box)
    {
        var target = group.Map.Boxes[box];
        var top = (Window)TopLevel.GetTopLevel(target)!;
        var at = CenterOf(target, top);
        top.MouseDown(at, MouseButton.Left);
        top.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    static void Hover(Control control)
    {
        var top = (Window)TopLevel.GetTopLevel(control)!;
        top.MouseMove(CenterOf(control, top));
        Dispatcher.UIThread.RunJobs();
    }

    static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    static string[] Notes(DockTabs group) => [.. group.Map.Boxes.Select(box => AutomationProperties.GetHelpText(box) ?? "")];

    static bool[] Open(DockTabs group) => [.. group.Map.Boxes.Select(box => box.IsEnabled)];

    static int[] Lit(DockTabs group) => [.. group.Map.Boxes.Select((box, at) => (box, at)).Where(each => each.box.Classes.Contains(PanelMap.Here)).Select(each => each.at)];

    static Window? FloatOf(DockTabs group, EditorWindow window) => TopLevel.GetTopLevel(group) is Window owner && owner != window ? owner : null;

    [Fact]
    public void Each_box_has_its_name_and_says_what_a_click_does_with_the_home_box_lit_and_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(["Left column, top", "Left column, bottom", "Right column, top", "Right column, bottom", PanelMap.PopOut], window.SceneDock.Map.Boxes.Select(box => AutomationProperties.GetName(box)));
            Assert.Equal(["It is here now", "Trades places with the panel there", "Trades places with the panel there", "Nothing is there yet", "Floats over the editor"], Notes(window.SceneDock));
            Assert.Equal([false, true, true, true, true], Open(window.SceneDock));
            Assert.Equal([UpperLeft], Lit(window.SceneDock));
            Assert.Equal([LowerLeft], Lit(window.FileDock));
            Assert.Equal([UpperRight], Lit(window.InspectorDock));
        });
    }

    [Fact]
    public void A_click_on_a_box_moves_the_panel_there_and_shuts_the_menu()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var right = BoxOf(window.InspectorDock, window);
            OpenMenu(window.SceneDock);
            Assert.True(MenuOf(window.SceneDock).IsOpen);

            PressBox(window.SceneDock, UpperRight);
            window.UpdateLayout();

            Assert.False(MenuOf(window.SceneDock).IsOpen);
            Assert.Equal(right, BoxOf(window.SceneDock, window));
            Assert.Equal("Moved Scene to the right column, top.", window.Status);
            Assert.Equal([UpperRight], Lit(window.SceneDock));
        });
    }

    [Fact]
    public void A_click_on_the_view_between_the_boxes_leaves_the_menu_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            OpenMenu(window.SceneDock);
            var map = window.SceneDock.Map;

            var between = map.TranslatePoint(new Point(map.Boxes[UpperLeft].Bounds.Width + 30, 30), window)!.Value;
            window.MouseDown(between, MouseButton.Left);
            window.MouseUp(between, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.True(MenuOf(window.SceneDock).IsOpen);
            Assert.Equal(DockSlot.UpperLeft, window.SceneDock.Slot);
        });
    }

    [Fact]
    public void The_words_under_the_map_name_the_box_under_the_pointer_even_when_it_is_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            OpenMenu(window.SceneDock);
            var map = window.SceneDock.Map;
            Assert.Equal([PanelMap.Hint], map.Words);

            Hover(map.Boxes[LowerRight]);
            Assert.Equal(["Right column, bottom", "Nothing is there yet"], map.Words);

            Hover(map.Boxes[UpperLeft]);
            Assert.Equal(["Left column, top", "It is here now"], map.Words);

            Hover(window.SceneDock.MenuButton);
            Assert.Equal([PanelMap.Hint], map.Words);
        });
    }

    [Fact]
    public void The_window_box_pops_the_panel_out_and_its_home_box_puts_it_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var dock = window.SceneDock;
            var box = BoxOf(dock, window);
            OpenMenu(dock);

            PressBox(dock, Floating);

            var floating = FloatOf(dock, window)!;
            floating.UpdateLayout();
            Assert.Equal(box.Size, dock.Bounds.Size);
            Assert.Equal("Scene", floating.Title);
            Assert.Same(window, floating.Owner);
            Assert.Equal([Floating], Lit(dock));
            Assert.Equal([true, true, true, true, false], Open(dock));
            Assert.Equal("Puts it back where it sat", Notes(dock)[UpperLeft]);
            Assert.Equal("Scene is in a window of its own. Close it to put it back.", window.Status);

            OpenMenu(dock);
            PressBox(dock, UpperLeft);
            window.UpdateLayout();

            Assert.Null(FloatOf(dock, window));
            Assert.Equal(box, BoxOf(dock, window));
            Assert.Equal([UpperLeft], Lit(dock));
        });
    }

    [Fact]
    public void A_locked_layout_turns_every_box_off_and_says_why()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.Docks.Locked = true;
            OpenMenu(window.SceneDock);

            Assert.Equal([false, false, false, false, false], Open(window.SceneDock));
            Assert.Equal(["It is here now", "The layout is locked", "The layout is locked", "The layout is locked", "The layout is locked"], Notes(window.SceneDock));
            Assert.Equal(["The layout is locked"], window.SceneDock.Map.Words);
        });
    }

    [Fact]
    public void A_floating_panel_can_still_go_back_while_the_layout_is_locked()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var dock = window.SceneDock;
            OpenMenu(dock);
            PressBox(dock, Floating);
            window.Docks.Locked = true;

            Assert.Equal([true, false, false, false, false], Open(dock));
            OpenMenu(dock);
            PressBox(dock, UpperLeft);

            Assert.Null(FloatOf(dock, window));
        });
    }

    [Fact]
    public void Left_and_right_walk_the_boxes_that_are_on_and_enter_moves_the_panel()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var map = window.SceneDock.Map;
            OpenMenu(window.SceneDock);

            // The first box is where the panel sits, so it is off and the walk starts at the next.
            Key(window, PhysicalKey.ArrowRight);
            Assert.True(map.Boxes[LowerLeft].IsFocused);
            Assert.Equal(["Left column, bottom", "Trades places with the panel there"], map.Words);

            Key(window, PhysicalKey.ArrowRight);
            Key(window, PhysicalKey.ArrowLeft);
            Assert.True(map.Boxes[LowerLeft].IsFocused);
            Key(window, PhysicalKey.ArrowLeft);
            Assert.True(map.Boxes[Floating].IsFocused);

            Key(window, PhysicalKey.ArrowRight);
            Key(window, PhysicalKey.ArrowRight);
            Assert.True(map.Boxes[UpperRight].IsFocused);
            Key(window, PhysicalKey.Enter);

            Assert.Equal(DockSlot.UpperRight, window.SceneDock.Slot);
            Assert.False(MenuOf(window.SceneDock).IsOpen);
        });
    }

    [Fact]
    public void Down_leaves_the_map_for_the_tabs_and_enter_on_the_map_itself_goes_into_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var map = window.SceneDock.Map;
            OpenMenu(window.SceneDock);

            Key(window, PhysicalKey.Enter);
            Assert.True(MenuOf(window.SceneDock).IsOpen);
            Assert.True(map.Boxes[LowerLeft].IsFocused);

            Key(window, PhysicalKey.ArrowDown);
            var focused = Assert.IsType<MenuItem>(window.FocusManager!.GetFocusedElement());
            Assert.Equal("Scene", AutomationProperties.GetName(focused));
            Assert.Equal([PanelMap.Hint], map.Words);
        });
    }
}
