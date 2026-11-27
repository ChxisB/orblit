using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class PanelCollapseTests
{
    static HoverCard CardOf(Control control) => Assert.IsType<HoverCard>(ToolTip.GetTip(control));

    static string[] Rail(DockSide side) => [.. side.RailButtons.Select(button => CardOf(button).Title)];

    static void Collapse(DockTabs group, EditorWindow window)
    {
        Click(window.Docks.CollapseButtonOf(group));
        window.UpdateLayout();
    }

    static void Choose(string workspace, EditorWindow window)
    {
        ClickOn(window.Bar.Tabs.Single(tab => tab.Label == workspace), window);
        window.UpdateLayout();
    }

    [Fact]
    public void A_collapsed_group_goes_to_a_rail_and_the_group_below_takes_the_room()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Collapse(window.SceneDock, window);

            Assert.False(window.SceneDock.IsEffectivelyVisible);
            Assert.Equal(["Scene"], Rail(window.LeftSide));
            Assert.False(window.LeftSide.IsFolded);
            AssertBox(8, 46, DockSide.RailWidth, 1225, BoxOf(window.LeftSide.Rail, window));
            AssertBox(60, 46, 280, 1225, BoxOf(window.FileDock, window));
        });
    }

    [Fact]
    public void A_rail_button_brings_its_group_back_where_it_was()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var scene = BoxOf(window.SceneDock, window);
            var files = BoxOf(window.FileDock, window);
            Collapse(window.SceneDock, window);

            Click(window.LeftSide.RailButtons.Single());
            window.UpdateLayout();

            Assert.Equal(scene, BoxOf(window.SceneDock, window));
            Assert.Equal(files, BoxOf(window.FileDock, window));
            Assert.False(window.LeftSide.Rail.IsVisible);
            Assert.Empty(window.LeftSide.RailButtons);
        });
    }

    [Fact]
    public void A_side_with_both_groups_collapsed_is_its_rail_alone()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Collapse(window.SceneDock, window);
            Collapse(window.FileDock, window);

            Assert.True(window.LeftSide.IsFolded);
            Assert.Equal(["Scene", "Files"], Rail(window.LeftSide));
            Assert.Equal(60, BoxOf(window.Center, window).X);
        });
    }

    [Fact]
    public void Every_panel_has_the_same_handle_half_over_its_inner_edge()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            foreach (var group in new[] { window.SceneDock, window.FileDock, window.InspectorDock })
            {
                var button = window.Docks.CollapseButtonOf(group);
                var panel = BoxOf(group, window);
                var edge = group.Slot.IsLeft() ? panel.Right : panel.X;
                AssertBox(edge - 12, panel.Y + DockTabs.StripHeight - 12, 24, 24, BoxOf(button, window));
                Assert.Equal("Chevrons", Assert.IsType<EditorIcon>(button.Content).IconName);
                Assert.Equal("Collapse to the side", CardOf(button).Title);
                Assert.Equal("Folds this panel to a button at the window's edge.", CardOf(button).Line);
                Assert.Equal("Collapse to the side", AutomationProperties.GetName(button));
            }
        });
    }

    // A real pointer, past the panel's edge, so the half outside is on top of what is beside the panel.
    [Fact]
    public void The_half_of_a_handle_outside_its_panel_collapses_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var left = BoxOf(window.SceneDock, window);
            var right = BoxOf(window.InspectorDock, window);

            foreach (var at in new[] { new Point(left.Right + 10, left.Y + DockTabs.StripHeight), new Point(right.X - 10, right.Y + DockTabs.StripHeight) })
            {
                window.MouseDown(at, MouseButton.Left);
                window.MouseUp(at, MouseButton.Left);
                window.UpdateLayout();
            }

            Assert.Equal(["Scene"], Rail(window.LeftSide));
            Assert.Equal(["Inspector"], Rail(window.RightSide));
        });
    }

    [Fact]
    public void A_floating_panel_has_no_handle()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var button = window.Docks.CollapseButtonOf(window.FileDock);

            window.FileDock.FloatItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            window.UpdateLayout();

            Assert.False(button.IsEffectivelyVisible);
        });
    }

    [Fact]
    public void A_rail_button_shows_the_icon_of_the_tab_the_group_was_on()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.FileDock.Select(1);

            Collapse(window.FileDock, window);

            var button = window.LeftSide.RailButtons.Single();
            Assert.Equal("History", Assert.IsType<EditorIcon>(button.Content).IconName);
            Assert.Equal("History", CardOf(button).Title);
        });
    }

    [Fact]
    public void Show_panels_brings_back_only_what_was_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Collapse(window.SceneDock, window);

            Click(window.SceneTabs.PanelsButton);
            Assert.True(window.LeftSide.IsFolded);
            Assert.True(window.RightSide.IsFolded);

            Click(window.SceneTabs.PanelsButton);
            window.UpdateLayout();

            Assert.False(window.SceneDock.IsEffectivelyVisible);
            Assert.True(window.FileDock.IsEffectivelyVisible);
            Assert.True(window.InspectorDock.IsEffectivelyVisible);
        });
    }

    [Fact]
    public void A_narrow_group_keeps_the_chosen_tab_and_moves_the_rest_to_More()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var group = window.SceneDock;
            group.Select(1);

            // Room for the chosen tab, the More button and the two strip buttons, and not for the other tab.
            group.Width = group.Tabs[1].Bounds.Width + 100;
            window.UpdateLayout();

            Assert.DoesNotContain(OverflowBar.Moved, group.Tabs[1].Classes);
            Assert.Contains(OverflowBar.Moved, group.Tabs[0].Classes);
            Assert.DoesNotContain(OverflowBar.Moved, group.Strip.MoreButton.Classes);

            var menu = (MenuFlyout)group.Strip.MoreButton.Flyout!;
            menu.ShowAt(group.Strip.MoreButton);
            var item = Assert.IsType<MenuItem>(Assert.Single(menu.Items));
            Assert.Equal("Scene", item.Header);
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(0, group.SelectedIndex);

            // A hidden tab takes no room, so nothing is left for More.
            group.ToggleTab("Import");
            window.UpdateLayout();
            Assert.Contains(OverflowBar.Moved, group.Strip.MoreButton.Classes);
        });
    }

    [Fact]
    public void A_group_moved_to_an_empty_place_leaves_the_other_the_whole_side()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Click(window.Docks.CollapseButtonOf(window.SceneDock));
            window.SceneDock.MoveItems[(int)DockSlot.LowerRight].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            window.UpdateLayout();

            AssertBox(8, 46, 280, 1225, BoxOf(window.FileDock, window));
            AssertBox(3116, 46, 316, 609, BoxOf(window.InspectorDock, window));
            AssertBox(3116, 663, 316, 608, BoxOf(window.SceneDock, window));
        });
    }

    [Fact]
    public void A_group_with_every_tab_off_gives_its_room_to_the_other()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.SceneDock.ToggleTab("Scene");
            window.SceneDock.ToggleTab("Import");
            window.UpdateLayout();

            AssertBox(8, 46, 280, 1225, BoxOf(window.FileDock, window));
            Assert.False(window.LeftSide.Rail.IsVisible);
        });
    }

    [Fact]
    public void A_workspace_that_folds_the_sides_leaves_the_3D_folds_as_they_were()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Collapse(window.SceneDock, window);

            Choose(TitleBar.Script, window);
            Assert.True(window.LeftSide.IsFolded);
            Click(window.LeftSide.RailButtons.First());
            Choose(TitleBar.Spatial, window);

            Assert.False(window.SceneDock.IsEffectivelyVisible);
            Assert.True(window.FileDock.IsEffectivelyVisible);
            Assert.True(window.InspectorDock.IsEffectivelyVisible);

            Choose(TitleBar.Script, window);
            Assert.True(window.LeftSide.IsFolded);
        });
    }

    [Fact]
    public void A_file_picked_to_import_opens_its_collapsed_group()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ImportText["res://rock.glb"] = ImportFileTests.Rock;
            var window = OpenAtReferenceSize(scene);
            Collapse(window.SceneDock, window);

            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Path == "res://rock.glb");
            window.UpdateLayout();

            Assert.True(window.SceneDock.IsEffectivelyVisible);
            Assert.Equal(1, window.SceneDock.SelectedIndex);
        });
    }
}
