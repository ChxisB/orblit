using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class MenusTests
{
    static void Press(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    // A heading is a text block, a described item a stack with its title first, and a separator a dash.
    static string HeaderText(object? item) => item switch
    {
        MenuItem { Header: string text } => text,
        MenuItem { Header: TextBlock block } => block.Text!,
        MenuItem { Header: Panel panel } => ((TextBlock)panel.Children[0]).Text!,
        _ => "-",
    };

    static (EditorWindow Window, FakeScene Scene) OpenOnLamp()
    {
        var scene = new FakeScene();
        var window = OpenAtReferenceSize(scene);
        window.Outline.SelectedItem = FakeScene.Lamp;
        return (window, scene);
    }

    [Fact]
    public void The_reset_menu_names_the_entity_and_the_collision_menu_describes_its_two_shapes()
    {
        UiThread.Run(() =>
        {
            var toolbar = OpenOnLamp().Window.Toolbar;
            var resetAll = (MenuItem)toolbar.ResetMenu.Items[^1]!;

            Assert.Equal(["RESET LAMP", "Reset Position", "Reset Rotation", "Reset Scale", "-", "Reset All"], toolbar.ResetMenu.Items.Select(HeaderText));
            Assert.Equal("⌥R", Parts.KeyText(resetAll.InputGesture!));
            Assert.Equal(
                ["ADD COLLISION TO THE MESH", "Create Convex Collision", "Create Trimesh Collision"],
                ((MenuFlyout)toolbar.CollisionButton.Flyout!).Items.Select(HeaderText));
        });
    }

    [Fact]
    public void Reset_and_focus_are_on_for_an_entity_with_a_place_alone()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var toolbar = window.Toolbar;
            Button[] buttons = [toolbar.ResetButton, toolbar.ResetAllButton, toolbar.FocusButton];
            bool On() => buttons.All(button => button.IsEnabled);
            bool Off() => buttons.All(button => !button.IsEnabled);
            Assert.True(Off());

            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.True(On());
            window.Outline.SelectedItem = FakeScene.Sound;
            Assert.True(Off());
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Outline.SelectedItem = null;
            Assert.True(Off());
        });
    }

    [Fact]
    public void Reset_all_puts_back_the_place_the_turn_and_the_size_from_the_item_and_from_alt_r()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = OpenOnLamp();

            Press((MenuItem)window.Toolbar.ResetMenu.Items[^1]!);
            Assert.Equal([Vector3.Zero, Vector3.Zero, Vector3.One], [scene.PositionOf(1), scene.RotationOf(1), scene.ScaleOf(1)]);
            Assert.Equal("Reset the transform of Lamp. Save keeps the change.", window.Status);

            scene.MoveTo(1, new Vector3(1, 2, 3));
            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.R, KeyModifiers = KeyModifiers.Alt });
            Assert.Equal(Vector3.Zero, scene.PositionOf(1));
            Assert.Equal(1, window.ScenePane.Selected!.Handle);
        });
    }

    [Fact]
    public void Reset_position_puts_the_entity_at_the_origin_and_keeps_it_selected()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = OpenOnLamp();

            Press(window.Toolbar.ResetPositionItem);

            Assert.Equal(Vector3.Zero, scene.PositionOf(1));
            Assert.Equal(new Vector3(10, 20, 30), scene.RotationOf(1));
            Assert.Equal(new Vector3(4, 5, 6), scene.ScaleOf(1));
            Assert.Equal("Reset the position of Lamp. Save keeps the change.", window.Status);
            Assert.Equal(1, window.ScenePane.Selected!.Handle);
            Assert.Equal(["0", "0", "0"], window.Inspector.Position.Axes.Select(box => box.Text));
        });
    }

    [Fact]
    public void Reset_rotation_and_reset_scale_each_change_their_own_value()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = OpenOnLamp();

            Press(window.Toolbar.ResetRotationItem);
            Assert.Equal([new Vector3(1, 2, 3), Vector3.Zero, new Vector3(4, 5, 6)], [scene.PositionOf(1), scene.RotationOf(1), scene.ScaleOf(1)]);
            Assert.Equal("Reset the rotation of Lamp. Save keeps the change.", window.Status);

            Press(window.Toolbar.ResetScaleItem);
            Assert.Equal([new Vector3(1, 2, 3), Vector3.Zero, Vector3.One], [scene.PositionOf(1), scene.RotationOf(1), scene.ScaleOf(1)]);
            Assert.Equal("Reset the scale of Lamp. Save keeps the change.", window.Status);
            Assert.Equal(["1", "1", "1"], window.Inspector.Scale.Axes.Select(box => box.Text));
        });
    }

    [Fact]
    public void The_snap_switches_start_on_and_a_switch_that_is_off_gives_no_step()
    {
        UiThread.Run(() =>
        {
            var toolbar = OpenAtReferenceSize().Toolbar;
            toolbar.SnapMoveBox.Text = "0.5";
            Assert.Equal(new GizmoSnap(0.5f, 15f, 0.1f), toolbar.Steps);

            toolbar.SnapPositionSwitch.IsChecked = false;
            Assert.Equal(new GizmoSnap(0f, 15f, 0.1f), toolbar.Steps);

            toolbar.SnapRotationSwitch.IsChecked = false;
            Assert.Equal(new GizmoSnap(0f, 0f, 0.1f), toolbar.Steps);
        });
    }

    [Fact]
    public void A_snap_step_that_is_not_a_positive_number_gives_the_default_step()
    {
        UiThread.Run(() =>
        {
            var toolbar = OpenAtReferenceSize().Toolbar;
            toolbar.SnapMoveBox.Text = "-2";

            Assert.Equal(GizmoSnap.Default, toolbar.Steps);
        });
    }

    [Fact]
    public void A_collision_item_asks_the_scene_for_a_shape_and_says_which()
    {
        UiThread.Run(() =>
        {
            var (window, mesh) = OpenMesh(out var scene);
            window.Outline.SelectedItem = mesh;

            Press(window.Toolbar.TrimeshItem);
            Assert.Equal("Made a trimesh collision shape under Box. Save keeps the change.", window.Status);
            Press(window.Toolbar.ConvexItem);
            Assert.Equal("Made a convex collision shape under Box. Save keeps the change.", window.Status);

            Assert.Equal([(mesh.Handle, false), (mesh.Handle, true)], scene.Collisions);
            Assert.Equal(mesh.Handle, window.ScenePane.Selected!.Handle);
        });
    }

    [Fact]
    public void A_collision_the_scene_refuses_shows_the_reason_and_changes_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = OpenOnLamp();

            Press(window.Toolbar.ConvexItem);

            Assert.Equal("Not a mesh instance.", window.Status);
            Assert.Empty(scene.Collisions);
        });
    }

    [Fact]
    public void The_projection_menu_switches_the_camera_and_the_label_and_checks_one_item()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var overlay = window.Center.Overlay;
            Assert.Equal("Perspective", overlay.ProjectionLabel);
            Assert.Equal([true, false], [overlay.PerspectiveItem.IsChecked, overlay.OrthogonalItem.IsChecked]);

            Press(overlay.OrthogonalItem);
            Assert.Equal("Orthogonal", overlay.ProjectionLabel);
            Assert.Equal([false, true], [overlay.PerspectiveItem.IsChecked, overlay.OrthogonalItem.IsChecked]);
            Assert.Equal("Orthogonal projection.", window.Status);

            Press(overlay.PerspectiveItem);
            Assert.Equal("Perspective", overlay.ProjectionLabel);
            Assert.Equal([true, false], [overlay.PerspectiveItem.IsChecked, overlay.OrthogonalItem.IsChecked]);
            Assert.Equal("Perspective projection.", window.Status);
            Assert.Equal([true, false], scene.Projections);
        });
    }

    [Fact]
    public void The_dock_menu_leaves_out_the_slot_the_dock_sits_in()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(["Move to the top", "Move to the bottom", "Move to the right"], window.SceneDock.MoveItems.Select(item => (string)item.Header!));
            Assert.Equal([false, true, true], window.SceneDock.MoveItems.Select(item => item.IsVisible));
            Assert.Equal([true, false, true], window.FileDock.MoveItems.Select(item => item.IsVisible));
            Assert.Equal([true, true, false], window.InspectorDock.MoveItems.Select(item => item.IsVisible));
        });
    }

    [Fact]
    public void Moving_a_dock_trades_places_with_the_dock_in_that_slot()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var sceneBox = BoxOf(window.SceneDock, window);
            var inspectorBox = BoxOf(window.InspectorDock, window);
            Assert.NotEqual(sceneBox, inspectorBox);

            Press(window.SceneDock.MoveItems[(int)DockSlot.Right]);
            window.UpdateLayout();

            Assert.Equal(inspectorBox, BoxOf(window.SceneDock, window));
            Assert.Equal(sceneBox, BoxOf(window.InspectorDock, window));
            Assert.Equal([DockSlot.Right, DockSlot.LowerLeft, DockSlot.UpperLeft], [window.SceneDock.Slot, window.FileDock.Slot, window.InspectorDock.Slot]);
            Assert.Equal([true, true, false], window.SceneDock.MoveItems.Select(item => item.IsVisible));
            Assert.Equal([false, true, true], window.InspectorDock.MoveItems.Select(item => item.IsVisible));
            Assert.Equal("Moved Scene to the right slot. The layout is not saved.", window.Status);
        });
    }

    [Fact]
    public void Two_docks_in_the_left_column_trade_places_and_the_move_can_be_undone()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var upper = BoxOf(window.SceneDock, window);
            var lower = BoxOf(window.FileDock, window);

            Press(window.FileDock.MoveItems[(int)DockSlot.UpperLeft]);
            window.UpdateLayout();
            Assert.Equal(upper, BoxOf(window.FileDock, window));
            Assert.Equal(lower, BoxOf(window.SceneDock, window));
            Assert.Equal("Moved FileSystem to the top slot. The layout is not saved.", window.Status);

            Press(window.SceneDock.MoveItems[(int)DockSlot.UpperLeft]);
            window.UpdateLayout();
            Assert.Equal(upper, BoxOf(window.SceneDock, window));
            Assert.Equal(lower, BoxOf(window.FileDock, window));
            Assert.Equal([DockSlot.UpperLeft, DockSlot.LowerLeft, DockSlot.Right], [window.SceneDock.Slot, window.FileDock.Slot, window.InspectorDock.Slot]);
        });
    }

    [Fact]
    public void A_dock_keeps_its_tab_and_its_content_when_it_moves()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.InspectorDock.Select(1);
            var content = window.InspectorDock.Content;

            Press(window.InspectorDock.MoveItems[(int)DockSlot.LowerLeft]);
            window.UpdateLayout();

            Assert.Equal(1, window.InspectorDock.SelectedIndex);
            Assert.Same(content, window.InspectorDock.Content);
            Assert.Contains(window.InspectorDock, content.GetVisualAncestors());
        });
    }

    static MenuItem DockItem(DockTabs dock, string header) =>
        ((MenuFlyout)dock.MenuButton.Flyout!).Items.OfType<MenuItem>().Single(item => item.Header as string == header);

    [Fact]
    public void The_dock_menu_lists_its_tabs_its_moves_float_and_reset()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = ((MenuFlyout)window.FileDock.MenuButton.Flyout!).Items.OfType<MenuItem>().ToList();

            // The map is the item with no text.
            Assert.Equal(
                ["FILESYSTEM DOCK SITS AT", null, "SHOW TABS", "FileSystem", "History", "Move to the top", "Move to the right", "Float", "Reset Layout"],
                items.Where(item => item.IsVisible).Select(item => item.Header as string ?? (item.Header as TextBlock)?.Text));
        });
    }

    [Fact]
    public void The_dock_map_lights_the_slot_the_dock_sits_in()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Assert.Equal([EditorBrushes.Accent, EditorBrushes.GuideLit, EditorBrushes.GuideLit], window.SceneDock.MapCells.Select(cell => cell.Background));

            Press(window.SceneDock.MoveItems[(int)DockSlot.Right]);

            Assert.Equal([EditorBrushes.GuideLit, EditorBrushes.GuideLit, EditorBrushes.Accent], window.SceneDock.MapCells.Select(cell => cell.Background));
            Assert.Equal([EditorBrushes.Accent, EditorBrushes.GuideLit, EditorBrushes.GuideLit], window.InspectorDock.MapCells.Select(cell => cell.Background));
        });
    }

    [Fact]
    public void A_tab_hidden_from_the_dock_menu_hands_the_body_to_a_tab_that_shows()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var dock = window.InspectorDock;
            var signals = DockItem(dock, "Signals");
            dock.Select(1);
            Assert.True(signals.IsChecked);

            Press(signals);

            Assert.False(dock.Tabs[1].IsVisible);
            Assert.False(signals.IsChecked);
            Assert.Equal(0, dock.SelectedIndex);
            Assert.True(dock.IsVisible);

            Press(DockItem(dock, "Inspector"));
            Assert.False(dock.IsVisible);

            Press(signals);
            Assert.True(dock.IsVisible);
            Assert.True(signals.IsChecked);
            Assert.Equal(1, dock.SelectedIndex);
        });
    }

    static Window? FloatOf(DockTabs dock, EditorWindow window) => TopLevel.GetTopLevel(dock) is Window owner && owner != window ? owner : null;

    [Fact]
    public void Float_puts_a_dock_in_its_own_window_and_dock_puts_it_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var dock = window.SceneDock;
            var box = BoxOf(dock, window);

            Press(dock.FloatItem);

            var floating = FloatOf(dock, window)!;
            Assert.Equal("Scene", floating.Title);
            Assert.Same(window, floating.Owner);
            Assert.Equal("Dock", dock.FloatItem.Header);
            Assert.Equal("Floated Scene. Close its window or choose Dock to put it back.", window.Status);

            Press(dock.FloatItem);
            window.UpdateLayout();

            Assert.Null(FloatOf(dock, window));
            Assert.Equal("Float", dock.FloatItem.Header);
            Assert.Equal(box, BoxOf(dock, window));
        });
    }

    [Fact]
    public void Closing_the_float_window_docks_the_dock_in_its_slot()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Press(window.InspectorDock.FloatItem);

            FloatOf(window.InspectorDock, window)!.Close();

            Assert.Null(FloatOf(window.InspectorDock, window));
            Assert.Contains(window.InspectorDock, window.GetVisualDescendants());
        });
    }

    [Fact]
    public void A_dock_sent_to_a_floating_dock_slot_trades_and_the_floating_dock_comes_back_in_the_other_slot()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var lower = BoxOf(window.FileDock, window);
            Press(window.SceneDock.FloatItem);

            Press(window.FileDock.MoveItems[(int)DockSlot.UpperLeft]);

            Assert.NotNull(FloatOf(window.SceneDock, window));
            Assert.Equal(DockSlot.LowerLeft, window.SceneDock.Slot);

            Press(window.SceneDock.FloatItem);
            window.UpdateLayout();

            Assert.Equal(lower, BoxOf(window.SceneDock, window));
            Assert.Equal(DockSlot.UpperLeft, window.FileDock.Slot);
        });
    }

    [Fact]
    public void Moving_or_resetting_a_floating_dock_docks_it_first()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var right = BoxOf(window.InspectorDock, window);
            Press(window.SceneDock.FloatItem);

            Press(window.SceneDock.MoveItems[(int)DockSlot.Right]);
            window.UpdateLayout();

            Assert.Null(FloatOf(window.SceneDock, window));
            Assert.Equal(right, BoxOf(window.SceneDock, window));

            Press(window.FileDock.FloatItem);
            Press(DockItem(window.InspectorDock, "Reset Layout"));

            Assert.Null(FloatOf(window.FileDock, window));
            Assert.Equal([DockSlot.UpperLeft, DockSlot.LowerLeft, DockSlot.Right], [window.SceneDock.Slot, window.FileDock.Slot, window.InspectorDock.Slot]);
        });
    }

    [Fact]
    public void Reset_layout_puts_every_dock_home_with_all_its_tabs()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var sceneBox = BoxOf(window.SceneDock, window);
            Press(window.SceneDock.MoveItems[(int)DockSlot.Right]);
            Press(DockItem(window.FileDock, "History"));
            Press(DockItem(window.FileDock, "FileSystem"));
            window.UpdateLayout();
            Assert.False(window.FileDock.IsVisible);

            Press(DockItem(window.InspectorDock, "Reset Layout"));
            window.UpdateLayout();

            Assert.Equal([DockSlot.UpperLeft, DockSlot.LowerLeft, DockSlot.Right], [window.SceneDock.Slot, window.FileDock.Slot, window.InspectorDock.Slot]);
            Assert.Equal(sceneBox, BoxOf(window.SceneDock, window));
            Assert.True(window.FileDock.IsVisible);
            Assert.All(window.FileDock.Tabs, tab => Assert.True(tab.IsVisible));
            Assert.Equal("Reset the layout. Every dock is back in its place with all its tabs.", window.Status);
        });
    }

    [Theory]
    [InlineData("Scene", "Nothing to import")]
    [InlineData("FileSystem", "Nothing to undo yet")]
    [InlineData("Inspector", "Nothing selected")]
    public void The_second_tab_of_each_dock_says_why_it_is_empty(string dock, string title)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tabs = new[] { window.SceneDock, window.FileDock, window.InspectorDock }.Single(each => each.Title == dock);

            tabs.Select(1);

            Assert.Contains(title, tabs.Content.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text));
        });
    }

    static (EditorWindow Window, OutlineNode Mesh) OpenMesh(out FakeScene scene)
    {
        scene = new FakeScene();
        var handle = scene.AddChild(FakeScene.Field.Handle, "MeshInstance3D", "Box");
        var window = OpenAtReferenceSize(scene);
        return (window, scene.Outline()!.Find(handle)!);
    }
}
