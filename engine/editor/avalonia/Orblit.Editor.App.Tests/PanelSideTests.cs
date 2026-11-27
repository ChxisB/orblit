using Avalonia.Controls;
using Avalonia.Interactivity;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class PanelSideTests
{
    static void Choose(string workspace, EditorWindow window)
    {
        ClickOn(window.Bar.Tabs.Single(tab => tab.Label == workspace), window);
        window.UpdateLayout();
    }

    static void PressPanels(EditorWindow window)
    {
        Click(window.SceneTabs.PanelsButton);
        window.UpdateLayout();
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

            Click(window.SceneDock.Map.Boxes[(int)DockSlot.LowerRight]);
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
        });
    }

    [Fact]
    public void Hidden_panels_give_their_room_to_the_middle()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            PressPanels(window);

            Assert.False(window.LeftSide.IsVisible);
            Assert.False(window.RightSide.IsVisible);
            Assert.Equal(8, BoxOf(window.Center, window).X);
            Assert.Equal(window.Bounds.Width - 16, BoxOf(window.Center, window).Width);
        });
    }

    [Fact]
    public void A_workspace_with_its_own_columns_starts_hidden_and_leaves_3D_as_the_user_had_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Choose(TitleBar.Script, window);
            Assert.False(window.SceneDock.IsEffectivelyVisible);
            PressPanels(window);
            Assert.True(window.SceneDock.IsEffectivelyVisible);
            Choose(TitleBar.Spatial, window);
            Assert.True(window.SceneDock.IsEffectivelyVisible);

            PressPanels(window);
            Choose(TitleBar.Script, window);
            PressPanels(window);
            Choose(TitleBar.Spatial, window);
            Assert.False(window.SceneDock.IsEffectivelyVisible);

            // Each visit starts hidden again.
            Choose(TitleBar.Script, window);
            Assert.False(window.SceneDock.IsEffectivelyVisible);
        });
    }

    [Fact]
    public void A_file_picked_to_import_shows_the_hidden_panels()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ImportText["res://rock.glb"] = ImportFileTests.Rock;
            var window = OpenAtReferenceSize(scene);
            PressPanels(window);

            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Path == "res://rock.glb");
            window.UpdateLayout();

            Assert.True(window.SceneDock.IsEffectivelyVisible);
            Assert.Equal(1, window.SceneDock.SelectedIndex);
        });
    }

    [Fact]
    public void A_panel_moved_while_the_panels_are_hidden_shows_them()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            PressPanels(window);

            Click(window.InspectorDock.Map.Boxes[(int)DockSlot.LowerRight]);
            window.UpdateLayout();

            Assert.True(window.InspectorDock.IsEffectivelyVisible);
        });
    }
}
