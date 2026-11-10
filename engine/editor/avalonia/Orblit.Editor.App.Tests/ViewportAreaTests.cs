using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ViewportAreaTests
{
    static bool NoFrame(out int width, out int height, ref byte[] rgba)
    {
        width = 0;
        height = 0;
        return false;
    }

    static (EditorWindow Window, ViewportControl Viewport) OpenWithViewport()
    {
        var window = OpenAtReferenceSize();
        var viewport = new ViewportControl(() => false, NoFrame);
        window.ShowViewport(viewport);
        window.UpdateLayout();
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, viewport);
    }

    static Border Pill(EditorWindow window, int index) => (Border)window.Toolbar.Children[index];

    static Control[] PillButtons(EditorWindow window, int index) =>
        [.. ((StackPanel)Pill(window, index).Child!).Children.OfType<Control>()];

    [Fact]
    public void The_scene_tab_joins_the_toolbar_and_shows_the_root()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var strip = window.SceneTabs;

            Assert.True(strip.Tab.IsVisible);
            Assert.Equal(287, BoxOf(strip.Tab, window).X);
            Assert.Equal(41, BoxOf(strip.Tab, window).Y);
            Assert.Equal(33, strip.Tab.Bounds.Height);
            Assert.Equal(EditorColors.Panel, ((ISolidColorBrush)strip.Tab.Background!).Color);
            Assert.Equal("Field", strip.Label.Text);
            Assert.Equal("Entity3D", strip.Icon.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Equal(EditorColors.Accent, ((ISolidColorBrush)strip.Label.Foreground!).Color);
        });
    }

    [Fact]
    public void The_icon_and_name_sit_where_the_reference_has_them()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var strip = window.SceneTabs;

            AssertBox(303, 49, 16, 16, BoxOf(strip.Icon.GetVisualDescendants().OfType<EditorIcon>().Single(), window));
            Assert.Equal(324, BoxOf(strip.Label, window).X);
        });
    }

    [Fact]
    public void The_close_button_ends_12_pixels_inside_the_tab_and_the_plus_starts_8_after_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var strip = window.SceneTabs;
            var tab = BoxOf(strip.Tab, window);

            Assert.Equal(24, strip.CloseButton.Bounds.Width);
            Assert.Equal(12, tab.Right - BoxOf(strip.CloseButton, window).Right);
            Assert.Equal(8, BoxOf(strip.AddButton, window).X - tab.Right);
            Assert.InRange(BoxOf(strip.CloseButton, window).X - BoxOf(strip.Label, window).Right, 8.5, 9.5);
            Assert.Equal(24, strip.AddButton.Bounds.Height);
        });
    }

    [Fact]
    public void The_two_menu_buttons_end_4_pixels_from_the_right_edge_with_8_between()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var strip = window.SceneTabs;

            Assert.Equal(3153 - 4, BoxOf(strip.DistractionButton, window).Right);
            Assert.Equal(8, BoxOf(strip.DistractionButton, window).X - BoxOf(strip.MenuButton, window).Right);
            Assert.Equal(24, strip.MenuButton.Bounds.Width);
        });
    }

    [Fact]
    public void With_no_scene_open_there_is_no_tab()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { StartsEmpty = true });

            Assert.False(window.SceneTabs.Tab.IsVisible);
        });
    }

    [Fact]
    public void Opening_a_scene_renames_the_tab()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            scene.Open("res://levels/second.oscn");

            window.Load(scene);

            Assert.Equal("second", window.SceneTabs.Label.Text);
        });
    }

    [Fact]
    public void Each_button_of_the_strip_answers_in_the_status()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var strip = window.SceneTabs;
            (Button Button, string Message)[] cases =
            [
                (strip.MenuButton, "The scene tab menu is not built yet."),
                (strip.DistractionButton, "Distraction free mode is not built yet."),
            ];

            foreach (var (button, message) in cases)
            {
                ClickOn(button, window);

                Assert.Equal(message, window.Status);
                window.Fail("");
            }
        });
    }

    [Fact]
    public void The_first_group_is_a_rounded_pill_with_five_modes_a_gap_and_two_tools()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pill = Pill(window, 0);
            var buttons = PillButtons(window, 0).OfType<FixedToggle>().ToArray();

            AssertBox(291, 79, 240, 32, BoxOf(pill, window));
            Assert.Equal(EditorColors.Field, ((ISolidColorBrush)pill.Background!).Color);
            Assert.Equal(EditorTheme.Corners, pill.CornerRadius);
            Assert.Equal([295, 327, 359, 391, 423, 467, 499], buttons.Select(b => BoxOf(b, window).X));
            Assert.All(buttons, button => Assert.Equal(new Size(28, 24), button.Bounds.Size));
            Assert.All(buttons, button => Assert.Equal(83, BoxOf(button, window).Y));
        });
    }

    [Fact]
    public void Only_the_transform_mode_is_lit()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var modes = PillButtons(window, 0).OfType<FixedToggle>().Take(5).ToArray();

            Assert.Equal([true, false, false, false, false], modes.Select(mode => mode.IsChecked == true));
            Assert.Same(window.Toolbar.TransformButton, modes[0]);
        });
    }

    [Fact]
    public void A_click_on_transform_move_or_select_lights_that_mode_alone()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var modes = PillButtons(window, 0).OfType<FixedToggle>().Take(5).ToArray();

            ClickOn(modes[1], window);
            Assert.Equal([false, true, false, false, false], modes.Select(mode => mode.IsChecked == true));
            ClickOn(modes[4], window);
            Assert.Equal([false, false, false, false, true], modes.Select(mode => mode.IsChecked == true));
            ClickOn(modes[0], window);
            Assert.Equal([true, false, false, false, false], modes.Select(mode => mode.IsChecked == true));
        });
    }

    [Fact]
    public void A_click_on_local_coordinates_flips_the_space_and_says_nothing_is_missing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var local = window.Toolbar.LocalButton;
            ClickOn(window.Toolbar.MoveButton, window);
            Assert.Equal(GizmoSpace.Global, window.Toolbar.Space);

            ClickOn(local, window);
            Assert.True(local.IsChecked);
            Assert.Equal(GizmoSpace.Local, window.Toolbar.Space);
            ClickOn(local, window);
            Assert.False(local.IsChecked);
            Assert.Equal(GizmoSpace.Global, window.Toolbar.Space);
            Assert.DoesNotContain("not built", window.Status);
        });
    }

    [Fact]
    public void A_click_on_rotate_lights_it_alone_and_says_nothing_is_missing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var modes = PillButtons(window, 0).OfType<FixedToggle>().Take(5).ToArray();
            ClickOn(modes[1], window);

            ClickOn(modes[2], window);

            Assert.Same(window.Toolbar.RotateButton, modes[2]);
            Assert.Equal([false, false, true, false, false], modes.Select(mode => mode.IsChecked == true));
            Assert.Equal(GizmoMode.Rotate, window.Toolbar.Mode);
            Assert.DoesNotContain("not built", window.Status);
        });
    }

    [Fact]
    public void A_click_on_scale_lights_it_alone_and_says_nothing_is_missing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var modes = PillButtons(window, 0).OfType<FixedToggle>().Take(5).ToArray();

            ClickOn(modes[3], window);

            Assert.Same(window.Toolbar.ScaleButton, modes[3]);
            Assert.Equal([false, false, false, true, false], modes.Select(mode => mode.IsChecked == true));
            Assert.Equal(GizmoMode.Scale, window.Toolbar.Mode);
            Assert.DoesNotContain("not built", window.Status);
        });
    }

    [Fact]
    public void The_lock_and_group_buttons_follow_the_pill_by_16_pixels_and_are_off_without_a_selection()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var toolbar = window.Toolbar;

            Assert.Equal(547, BoxOf(toolbar.LockButton, window).X);
            Assert.Equal(579, BoxOf(toolbar.GroupButton, window).X);
            Assert.False(toolbar.LockButton.IsEnabled);
            Assert.False(toolbar.GroupButton.IsEnabled);
        });
    }

    [Fact]
    public void A_selection_turns_lock_and_group_on_and_clearing_it_turns_them_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.True(window.Toolbar.LockButton.IsEnabled);
            Assert.True(window.Toolbar.GroupButton.IsEnabled);

            window.Outline.SelectedItem = null;
            Assert.False(window.Toolbar.LockButton.IsEnabled);
            Assert.False(window.Toolbar.GroupButton.IsEnabled);
        });
    }

    [Fact]
    public void A_new_toolbar_starts_with_lock_and_group_off()
    {
        UiThread.Run(() =>
        {
            var toolbar = new ViewportToolbar(new Button());

            Assert.False(toolbar.LockButton.IsEnabled);
            Assert.False(toolbar.GroupButton.IsEnabled);
        });
    }

    [Fact]
    public void The_three_option_buttons_follow_with_a_gap_of_16()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var options = ((StackPanel)window.Toolbar.Children[2]).Children.Cast<FixedToggle>().ToArray();

            Assert.Equal([623, 655, 687], options.Select(option => BoxOf(option, window).X));
            Assert.All(options, option => Assert.False(option.IsChecked));
        });
    }

    [Fact]
    public void The_snap_group_is_a_pill_with_a_toggle_and_a_24_wide_menu_button()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pill = Pill(window, 3);
            var parts = PillButtons(window, 3);

            AssertBox(731, 79, 64, 32, BoxOf(pill, window));
            Assert.IsType<FixedToggle>(parts[0]);
            Assert.Equal(28, parts[0].Bounds.Width);
            Assert.Equal(24, parts[1].Bounds.Width);
        });
    }

    [Fact]
    public void The_preview_group_lights_the_environment_and_not_the_sun()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pill = Pill(window, 4);
            var parts = PillButtons(window, 4);

            AssertBox(814, 79, 96, 32, BoxOf(pill, window));
            Assert.False(((FixedToggle)parts[0]).IsChecked);
            Assert.True(((FixedToggle)parts[1]).IsChecked);
            Assert.Equal(24, parts[2].Bounds.Width);
        });
    }

    [Fact]
    public void A_menu_button_of_a_pill_says_it_is_not_built()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            ClickOn(PillButtons(window, 4)[2], window);

            Assert.Equal("Preview settings are not built yet.", window.Status);
        });
    }

    [Fact]
    public void The_mode_pill_shows_the_icons_of_the_old_toolbar_in_order()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(
                ["ToolTransform", "ToolMove", "ToolRotate", "ToolScale", "ToolSelect", "ListSelect", "Ruler"],
                PillButtons(window, 0).OfType<FixedToggle>().Select(button => ((EditorIcon)button.Content!).IconName));
        });
    }

    [Fact]
    public void The_transform_and_view_menus_follow_the_last_pill_as_text()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var toolbar = window.Toolbar;

            Assert.Equal(926, BoxOf(toolbar.TransformMenuButton, window).X);
            Assert.Equal(2, BoxOf(toolbar.ViewButton, window).X - BoxOf(toolbar.TransformMenuButton, window).Right);
            Assert.Equal("Transform", toolbar.TransformMenuButton.Content);
            Assert.Equal("View", toolbar.ViewButton.Content);
        });
    }

    static (EditorWindow Window, OutlineNode Mesh) OpenWithMesh()
    {
        var scene = new FakeScene();
        var handle = scene.AddChild(FakeScene.Field.Handle, "MeshInstance3D", "Box");
        var window = OpenAtReferenceSize(scene);
        return (window, scene.Outline()!.Find(handle)!);
    }

    [Fact]
    public void The_mesh_menu_shows_for_a_mesh_alone()
    {
        UiThread.Run(() =>
        {
            var (window, mesh) = OpenWithMesh();
            var toolbar = window.Toolbar;
            bool[] Shown() => [toolbar.MeshMenuButton.IsVisible, toolbar.MeshGap.IsVisible];

            Assert.Equal([false, false], Shown());
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.Equal([false, false], Shown());
            window.Outline.SelectedItem = mesh;
            Assert.Equal([true, true], Shown());
            window.Outline.SelectedItem = null;
            Assert.Equal([false, false], Shown());
        });
    }

    [Fact]
    public void The_mesh_menu_is_a_chip_32_high_after_a_thin_bar_that_follows_view()
    {
        UiThread.Run(() =>
        {
            var (window, mesh) = OpenWithMesh();
            window.Outline.SelectedItem = mesh;
            window.UpdateLayout();
            var toolbar = window.Toolbar;
            var view = BoxOf(toolbar.ViewButton, window);
            var bar = BoxOf(toolbar.MeshGap, window);
            var chip = BoxOf(toolbar.MeshMenuButton, window);
            var icon = BoxOf(toolbar.MeshMenuButton.GetVisualDescendants().OfType<EditorIcon>().Single(), window);
            var text = BoxOf(toolbar.MeshMenuButton.GetVisualDescendants().OfType<TextBlock>().Single(), window);

            // The widths follow the font, so the distances are measured from the neighbor.
            Assert.Equal([79.0, 32.0, 83.0, 24.0, 1.0], [chip.Y, chip.Height, bar.Y, bar.Height, bar.Width]);
            Assert.Equal(view.Right + 7, bar.X);
            Assert.Equal(bar.Right + 8, chip.X);
            Assert.Equal(chip.X + 6, icon.X);
            Assert.Equal(chip.X + 28, text.X);
            Assert.Equal(chip.Right - 8, text.Right);
            Assert.Equal(EditorBrushes.Chip, toolbar.MeshMenuButton.Background);
            Assert.Equal(EditorBrushes.BarSeparator, toolbar.MeshGap.Background);
        });
    }

    [Fact]
    public void The_menu_labels_are_centred_on_the_icons_of_the_pills()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var iconCentre = BoxOf(PillButtons(window, 0)[0], window).Center.Y;

            foreach (var button in new[] { window.Toolbar.TransformMenuButton, window.Toolbar.ViewButton })
            {
                var label = button.GetVisualDescendants().OfType<TextBlock>().Single();
                Assert.Equal(VerticalAlignment.Center, button.VerticalContentAlignment);
                Assert.Equal(iconCentre, BoxOf(button, window).Center.Y);
                Assert.Equal(iconCentre, BoxOf(label, window).Center.Y);
            }
        });
    }

    [Fact]
    public void The_view_menu_opens_and_its_focus_item_is_off_without_a_framable_selection()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            ClickOn(window.Toolbar.ViewButton, window);

            Assert.True(window.Toolbar.ViewButton.Flyout!.IsOpen);
            Assert.False(window.Toolbar.FocusItem.IsEnabled);
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.True(window.Toolbar.FocusItem.IsEnabled);
            window.Outline.SelectedItem = FakeScene.Sound;
            Assert.False(window.Toolbar.FocusItem.IsEnabled);
        });
    }

    [Fact]
    public void Focus_selection_frames_the_selected_entity()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Toolbar.FocusItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal([FakeScene.Lamp.Handle], scene.Framed);
            Assert.Equal("Framed Lamp.", window.Status);
        });
    }

    [Fact]
    public void Save_and_frame_have_no_place_on_the_toolbar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.DoesNotContain(window.SaveButton, window.GetVisualDescendants());
            Assert.DoesNotContain(window.FrameButton, window.GetVisualDescendants());
        });
    }

    [Fact]
    public void The_overlay_waits_for_a_viewport()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Assert.False(window.Center.Overlay.IsVisible);

            window.ShowViewport(new ViewportControl(() => false, NoFrame));

            Assert.True(window.Center.Overlay.IsVisible);
        });
    }

    [Fact]
    public void The_projection_button_sits_10_pixels_in_from_the_viewport_corner()
    {
        UiThread.Run(() =>
        {
            var (window, _) = OpenWithViewport();
            var button = window.Center.Overlay.ProjectionButton;

            Assert.Equal(297, BoxOf(button, window).X);
            Assert.Equal(126, BoxOf(button, window).Y);
            Assert.Equal(32, button.Bounds.Height);
            var icon = BoxOf(button.GetVisualDescendants().OfType<EditorIcon>().Single(), window);
            Assert.Equal(305, icon.X);
            Assert.Equal(new Size(16, 16), icon.Size);
            Assert.Equal(Color.FromArgb(0x5c, 0, 0, 0), ((ISolidColorBrush)button.Background!).Color);
        });
    }

    [Fact]
    public void The_axes_are_100_pixels_square_with_their_middle_50_in_from_the_top_right_corner()
    {
        UiThread.Run(() =>
        {
            var (window, _) = OpenWithViewport();

            AssertBox(3053, 116, 100, 100, BoxOf(window.Center.Overlay.Axes, window));
        });
    }

    [Theory]
    [InlineData(3103, 166)]
    [InlineData(700, 140)]
    public void A_drag_that_starts_on_the_axes_or_beside_the_button_reaches_the_viewport(double x, double y)
    {
        UiThread.Run(() =>
        {
            var (window, viewport) = OpenWithViewport();
            var orbits = 0;
            viewport.Orbited += (_, _) => orbits++;

            window.MouseDown(new Point(x, y), MouseButton.Left);
            window.MouseMove(new Point(x + 30, y + 30));

            Assert.Equal(1, orbits);
        });
    }

    [Fact]
    public void A_drag_in_the_viewport_turns_the_axes_like_the_camera()
    {
        UiThread.Run(() =>
        {
            var (window, viewport) = OpenWithViewport();
            var from = viewport.TranslatePoint(new Point(500, 500), window)!.Value;

            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(from + new Point(50, 10));

            Assert.Equal(OrbitView.Start.Orbited(-0.5f, 0.1f), window.Center.Overlay.Axes.View);
        });
    }

    [Fact]
    public void Looking_straight_ahead_puts_x_right_y_up_and_z_at_the_viewer()
    {
        var ends = AxisGizmo.Ends(OrbitView.Start with { Yaw = 0, Pitch = 0 });

        Near(new Point(79, 50), ends[0].At);
        Near(new Point(50, 21), ends[1].At);
        Near(new Point(50, 50), ends[2].At);
        Assert.Equal(["X", "Y", "Z", "-X", "-Y", "-Z"], ends.Select(end => end.Name));
        Assert.Equal(1f, ends[2].Depth, 1e-4f);
        Assert.Equal(-1f, ends[5].Depth, 1e-4f);
        Near(new Point(21, 50), ends[3].At);
        Near(new Point(50, 79), ends[4].At);
    }

    [Fact]
    public void A_quarter_turn_swings_z_to_the_left_and_x_to_the_viewer()
    {
        var ends = AxisGizmo.Ends(OrbitView.Start with { Yaw = MathF.PI / 2, Pitch = 0 });

        Near(new Point(21, 50), ends[2].At);
        Assert.Equal(1f, ends[0].Depth, 1e-4f);
    }

    [Fact]
    public void Looking_down_puts_z_below_the_middle_and_shortens_y()
    {
        var pitch = MathF.PI / 6;
        var ends = AxisGizmo.Ends(OrbitView.Start with { Yaw = 0, Pitch = pitch });

        Near(new Point(50, 50 + AxisGizmo.Reach * MathF.Sin(pitch)), ends[2].At);
        Near(new Point(50, 50 - AxisGizmo.Reach * MathF.Cos(pitch)), ends[1].At);
        Assert.Equal(MathF.Cos(pitch), ends[2].Depth, 1e-4f);
        Assert.Equal(MathF.Sin(pitch), ends[1].Depth, 1e-4f);
    }

    [Theory]
    [InlineData(0.6f, 0.4f)]
    [InlineData(-2f, 0.9f)]
    [InlineData(3f, -0.7f)]
    public void The_ends_agree_with_a_camera_that_looks_at_the_target(float yaw, float pitch)
    {
        var view = OrbitView.Start with { Yaw = yaw, Pitch = pitch };
        var back = Vector3.Normalize(view.Eye - view.Target);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, back));
        var up = Vector3.Cross(back, right);
        Vector3[] directions = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitX, -Vector3.UnitY, -Vector3.UnitZ];

        foreach (var (end, direction) in AxisGizmo.Ends(view).Zip(directions))
        {
            Near(new Point(50 + Vector3.Dot(direction, right) * AxisGizmo.Reach, 50 - Vector3.Dot(direction, up) * AxisGizmo.Reach), end.At);
            Assert.Equal(Vector3.Dot(direction, back), end.Depth, 1e-4f);
        }
    }

    [Fact]
    public void A_scene_whose_class_has_no_icon_gets_the_plain_entity_icon()
    {
        UiThread.Run(() =>
        {
            var strip = new SceneTabStrip();

            strip.Show(new OutlineNode(1, "Odd", "NoSuchClass", []), "odd");

            Assert.Equal("Entity", Assert.IsType<EditorIcon>(strip.Icon.Content).IconName);
        });
    }

    static void Near(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, 1e-3);
        Assert.Equal(expected.Y, actual.Y, 1e-3);
    }
}
