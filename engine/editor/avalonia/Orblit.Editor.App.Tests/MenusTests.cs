using System.Numerics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class MenusTests
{
    static void Press(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static string[] Headers(Button button) => [.. ((MenuFlyout)button.Flyout!).Items.Cast<MenuItem>().Select(item => (string)item.Header!)];

    static (EditorWindow Window, FakeScene Scene) OpenOnLamp()
    {
        var scene = new FakeScene();
        var window = OpenAtReferenceSize(scene);
        window.Outline.SelectedItem = FakeScene.Lamp;
        return (window, scene);
    }

    [Fact]
    public void The_transform_menu_lists_its_five_actions_and_the_mesh_menu_its_two()
    {
        UiThread.Run(() =>
        {
            var toolbar = OpenAtReferenceSize().Toolbar;

            Assert.Equal(
                ["Reset Position", "Reset Rotation", "Reset Scale", "Snap Position to Steps", "Snap Rotation to Steps"],
                Headers(toolbar.TransformMenuButton));
            Assert.Equal(["Create Trimesh Collision", "Create Convex Collision"], Headers(toolbar.MeshMenuButton));
        });
    }

    [Fact]
    public void The_transform_menu_is_on_for_an_entity_with_a_place_alone()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var button = window.Toolbar.TransformMenuButton;
            Assert.False(button.IsEnabled);

            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.True(button.IsEnabled);
            window.Outline.SelectedItem = FakeScene.Sound;
            Assert.False(button.IsEnabled);
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Outline.SelectedItem = null;
            Assert.False(button.IsEnabled);
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
    public void Snap_position_rounds_each_axis_to_the_move_step_in_the_snap_options()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = OpenOnLamp();
            scene.MoveTo(1, new Vector3(1.3f, 2.7f, -3.2f));
            window.Toolbar.SnapMoveBox.Text = "0.5";

            Press(window.Toolbar.SnapPositionItem);

            Assert.Equal(new Vector3(1.5f, 2.5f, -3f), scene.PositionOf(1));
            Assert.Equal("Snapped the position of Lamp. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void Snap_rotation_rounds_each_angle_to_the_turn_step_and_leaves_the_rest_alone()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = OpenOnLamp();

            Press(window.Toolbar.SnapRotationItem);

            Assert.Equal(new Vector3(15, 15, 30), scene.RotationOf(1));
            Assert.Equal(new Vector3(1, 2, 3), scene.PositionOf(1));
            Assert.Equal(new Vector3(4, 5, 6), scene.ScaleOf(1));
            Assert.Equal("Snapped the rotation of Lamp. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void A_snap_step_that_is_not_a_positive_number_snaps_to_the_default_step()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = OpenOnLamp();
            scene.MoveTo(1, new Vector3(1.3f, 2.7f, 3.6f));
            window.Toolbar.SnapMoveBox.Text = "-2";

            Press(window.Toolbar.SnapPositionItem);

            Assert.Equal(new Vector3(1, 3, 4), scene.PositionOf(1));
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

            Assert.Equal(["Move to Upper Left", "Move to Lower Left", "Move to Right"], window.SceneDock.MoveItems.Select(item => (string)item.Header!));
            Assert.Equal([false, true, true], window.SceneDock.MoveItems.Select(item => item.IsEnabled));
            Assert.Equal([true, false, true], window.FileDock.MoveItems.Select(item => item.IsEnabled));
            Assert.Equal([true, true, false], window.InspectorDock.MoveItems.Select(item => item.IsEnabled));
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
            Assert.Equal([true, true, false], window.SceneDock.MoveItems.Select(item => item.IsEnabled));
            Assert.Equal([false, true, true], window.InspectorDock.MoveItems.Select(item => item.IsEnabled));
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
            Assert.Equal("Moved FileSystem to the upper left slot. The layout is not saved.", window.Status);

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

    static (EditorWindow Window, OutlineNode Mesh) OpenMesh(out FakeScene scene)
    {
        scene = new FakeScene();
        var handle = scene.AddChild(FakeScene.Field.Handle, "MeshInstance3D", "Box");
        var window = OpenAtReferenceSize(scene);
        return (window, scene.Outline()!.Find(handle)!);
    }
}
