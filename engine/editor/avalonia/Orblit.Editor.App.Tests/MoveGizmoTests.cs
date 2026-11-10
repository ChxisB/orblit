using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;
using Vector2 = System.Numerics.Vector2;

namespace Orblit.Editor.App.Tests;

public sealed class MoveGizmoTests
{
    // A 100 by 50 frame in a 200 by 100 control is drawn at twice the size, so a frame pixel is two control pixels.
    // The pivot is at (40, 50) in the control, the X handle runs right to (100, 50) and the Y handle up to (40, 0).
    // The Z axis points at the camera and shows as a dot.
    static readonly GizmoHandles Handles = new(new Vector2(20, 25), new Vector2(50, 25), new Vector2(20, 0), new Vector2(20, 25));

    static readonly Size FrameSize = new(100, 50);

    static bool Frame(out int width, out int height, ref byte[] rgba)
    {
        width = 100;
        height = 50;
        rgba = new byte[width * height * 4];
        return true;
    }

    sealed class Rig
    {
        public required Window Window { get; init; }

        public required ViewportControl Viewport { get; init; }

        public List<(GizmoAxis Axis, Vector2 Pixels)> Drags { get; } = [];

        public List<Vector2> Clicks { get; } = [];

        public List<(float Yaw, float Pitch)> Orbits { get; } = [];
    }

    static Rig Shown(Func<GizmoHandles?>? source)
    {
        var viewport = new ViewportControl(() => false, Frame) { HandlesSource = source, ModeSource = () => GizmoMode.Move };
        var rig = new Rig { Window = new Window { Width = 200, Height = 100, Content = viewport }, Viewport = viewport };
        viewport.GizmoDragged += drag => rig.Drags.Add((drag.Hit.Axis, drag.Move.Delta));
        viewport.Clicked += rig.Clicks.Add;
        viewport.Orbited += (yaw, pitch) => rig.Orbits.Add((yaw, pitch));
        rig.Window.Show();
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return rig;
    }

    static Rig ShownWithHandles() => Shown(() => Handles);

    [Fact]
    public void A_handle_is_80_pixels_long_in_the_direction_the_axis_shows_on_screen()
    {
        Assert.Equal(new Vector2(100, 20), GizmoMath.Tip(new Vector2(20, 20), new Vector2(30, 20)));
        Assert.Equal(new Vector2(68, 84), GizmoMath.Tip(new Vector2(20, 20), new Vector2(23, 24)));
    }

    [Fact]
    public void An_axis_that_shows_as_a_dot_has_a_handle_of_no_length()
    {
        Assert.Equal(new Vector2(20, 20), GizmoMath.Tip(new Vector2(20, 20), new Vector2(20.5f, 20)));
    }

    [Fact]
    public void Each_axis_has_its_own_direction_and_tip()
    {
        Assert.Equal(Vector3.UnitX, GizmoMath.Axis(GizmoAxis.X));
        Assert.Equal(Vector3.UnitY, GizmoMath.Axis(GizmoAxis.Y));
        Assert.Equal(Vector3.UnitZ, GizmoMath.Axis(GizmoAxis.Z));
        var handles = new GizmoHandles(new Vector2(0, 0), new Vector2(1, 1), new Vector2(2, 2), new Vector2(3, 3));
        Assert.Equal(new Vector2(1, 1), handles.Tip(GizmoAxis.X));
        Assert.Equal(new Vector2(2, 2), handles.Tip(GizmoAxis.Y));
        Assert.Equal(new Vector2(3, 3), handles.Tip(GizmoAxis.Z));
    }

    // A yaw of 90 degrees: the local X is the global minus Z, and the local Z is the global X. Reading a row
    // instead of a column would give the opposite signs.
    static readonly Transform3D Yawed = new(new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(-1, 0, 0), Vector3.Zero);

    [Fact]
    public void A_local_axis_is_a_column_of_the_basis_and_a_global_axis_ignores_it()
    {
        var local = (GizmoAxis axis) => GizmoMath.Axis(new GizmoDirection(axis, GizmoSpace.Local), Yawed);
        var global = (GizmoAxis axis) => GizmoMath.Axis(new GizmoDirection(axis, GizmoSpace.Global), Yawed);

        Assert.Equal(new Vector3(0, 0, -1), local(GizmoAxis.X));
        Assert.Equal(new Vector3(0, 1, 0), local(GizmoAxis.Y));
        Assert.Equal(new Vector3(1, 0, 0), local(GizmoAxis.Z));
        Assert.Equal(Vector3.UnitX, global(GizmoAxis.X));
        Assert.Equal(Vector3.UnitY, global(GizmoAxis.Y));
        Assert.Equal(Vector3.UnitZ, global(GizmoAxis.Z));
    }

    [Fact]
    public void A_local_axis_has_length_one_however_the_entity_is_scaled_and_a_collapsed_axis_falls_back_to_global()
    {
        var scaled = new Transform3D(new Vector3(0, 0, 4), new Vector3(0, 0.5f, 0), new Vector3(-3, 0, 0), Vector3.Zero);
        var flat = new Transform3D(Vector3.Zero, new Vector3(0, 1, 0), new Vector3(0, 0, 1), Vector3.Zero);

        Assert.Equal(new Vector3(0, 0, -1), GizmoMath.Axis(new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), scaled));
        Assert.Equal(new Vector3(0, 1, 0), GizmoMath.Axis(new GizmoDirection(GizmoAxis.Y, GizmoSpace.Local), scaled));
        Assert.Equal(new Vector3(1, 0, 0), GizmoMath.Axis(new GizmoDirection(GizmoAxis.Z, GizmoSpace.Local), scaled));
        Assert.Equal(Vector3.UnitX, GizmoMath.Axis(new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), flat));
    }

    [Theory]
    [InlineData(10, 5, 100, 50, 200, 100, 20, 10)]
    [InlineData(0, 0, 100, 100, 200, 100, 50, 0)]
    [InlineData(50, 50, 100, 100, 200, 100, 100, 50)]
    public void A_pixel_of_the_frame_is_a_point_of_the_control(float x, float y, double fw, double fh, double bw, double bh, float px, float py)
    {
        var at = ViewportControl.ControlAt(new Vector2(x, y), new Size(fw, fh), new Size(bw, bh));

        Assert.Equal(new Vector2(px, py), at);
        Assert.Equal(new Vector2(x, y), ViewportControl.FrameAt(new Point(px, py), new Size(fw, fh), new Size(bw, bh)));
    }

    [Fact]
    public void An_empty_frame_puts_every_pixel_at_the_corner()
    {
        Assert.Equal(Vector2.Zero, ViewportControl.ControlAt(new Vector2(10, 10), new Size(0, 0), new Size(200, 100)));
    }

    [Theory]
    [InlineData(80, 50, GizmoAxis.X)]
    [InlineData(70, 55, GizmoAxis.X)]
    [InlineData(100, 50, GizmoAxis.X)]
    [InlineData(51, 50, GizmoAxis.X)]
    [InlineData(40, 20, GizmoAxis.Y)]
    [InlineData(45, 30, GizmoAxis.Y)]
    public void A_press_on_a_handle_takes_it(double x, double y, GizmoAxis axis)
    {
        UiThread.Run(() =>
        {
            Assert.Equal(axis, ShownWithHandles().Viewport.HandleAt(new Point(x, y)));
        });
    }

    [Theory]
    [InlineData(80, 60)]
    [InlineData(110, 50)]
    [InlineData(150, 80)]
    [InlineData(45, 50)]
    [InlineData(49, 50)]
    [InlineData(40, 59)]
    [InlineData(30, 50)]
    public void A_press_off_the_handles_or_on_the_pivot_takes_none(double x, double y)
    {
        UiThread.Run(() =>
        {
            Assert.Null(ShownWithHandles().Viewport.HandleAt(new Point(x, y)));
        });
    }

    [Fact]
    public void A_press_takes_nothing_when_there_are_no_handles()
    {
        UiThread.Run(() =>
        {
            Assert.Null(Shown(() => null).Viewport.HandleAt(new Point(80, 50)));
            Assert.Null(Shown(null).Viewport.HandleAt(new Point(80, 50)));
        });
    }

    [Fact]
    public void An_axis_that_shows_as_a_dot_takes_no_press()
    {
        UiThread.Run(() =>
        {
            var dot = new GizmoHandles(new Vector2(20, 25), new Vector2(20, 25), new Vector2(20, 25), new Vector2(20, 25));

            Assert.Null(Shown(() => dot).Viewport.HandleAt(new Point(60, 50)));
        });
    }

    [Fact]
    public void The_nearest_handle_takes_a_press_between_two()
    {
        UiThread.Run(() =>
        {
            // The X handle runs along y 50 and this Y handle slants to (100, 62), so both reach a press near (90, 55).
            var crossing = new GizmoHandles(new Vector2(20, 25), new Vector2(50, 25), new Vector2(50, 31), new Vector2(20, 25));
            var viewport = Shown(() => crossing).Viewport;

            Assert.Equal(GizmoAxis.Y, viewport.HandleAt(new Point(90, 57)));
            Assert.Equal(GizmoAxis.X, viewport.HandleAt(new Point(90, 52)));
        });
    }

    [Fact]
    public void The_source_is_asked_on_each_tick()
    {
        UiThread.Run(() =>
        {
            var asked = 0;
            var rig = Shown(() => { asked++; return Handles; });
            Assert.Equal(1, asked);

            rig.Viewport.Tick();

            Assert.Equal(2, asked);
        });
    }

    [Fact]
    public void A_drag_that_starts_on_a_handle_raises_the_move_in_frame_pixels_and_does_not_orbit()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithHandles();

            rig.Window.MouseDown(new Point(80, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(90, 50));
            rig.Window.MouseMove(new Point(90, 60));
            rig.Window.MouseUp(new Point(90, 60), MouseButton.Left);

            // The control draws at twice the frame, so 10 control pixels are 5 frame pixels. Each event is the
            // move since the one before.
            Assert.Equal([(GizmoAxis.X, new Vector2(5, 0)), (GizmoAxis.X, new Vector2(0, 5))], rig.Drags);
            Assert.Empty(rig.Orbits);
            Assert.Empty(rig.Clicks);
        });
    }

    [Fact]
    public void A_handle_moves_from_the_first_pixel()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithHandles();

            rig.Window.MouseDown(new Point(40, 20), MouseButton.Left);
            rig.Window.MouseMove(new Point(40, 22));

            Assert.Equal([(GizmoAxis.Y, new Vector2(0, 1))], rig.Drags);
        });
    }

    [Fact]
    public void A_press_on_the_pivot_still_clicks_and_orbits_and_moves_nothing()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithHandles();
            rig.Window.MouseDown(new Point(40, 50), MouseButton.Left);
            rig.Window.MouseUp(new Point(40, 50), MouseButton.Left);

            Assert.Equal([new Vector2(20, 25)], rig.Clicks);

            rig.Window.MouseDown(new Point(40, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(60, 50));

            Assert.Single(rig.Orbits);
            Assert.Empty(rig.Drags);
        });
    }

    [Fact]
    public void A_press_in_empty_space_orbits_while_handles_show()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithHandles();

            rig.Window.MouseDown(new Point(150, 80), MouseButton.Left);
            rig.Window.MouseMove(new Point(170, 80));

            Assert.Single(rig.Orbits);
            Assert.Empty(rig.Drags);
        });
    }

    [Fact]
    public void Letting_go_of_a_handle_ends_the_grab()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithHandles();
            rig.Window.MouseDown(new Point(80, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(85, 50));
            rig.Window.MouseUp(new Point(85, 50), MouseButton.Left);

            rig.Window.MouseDown(new Point(150, 80), MouseButton.Left);
            rig.Window.MouseMove(new Point(170, 80));

            Assert.Single(rig.Drags);
            Assert.Single(rig.Orbits);
        });
    }

    // The pointer stays captured when the window shrinks mid-drag. A frame of no width has no scale to turn pixels back.
    [Fact]
    public void A_control_that_shrinks_to_nothing_mid_drag_reports_no_move()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithHandles();
            rig.Window.MouseDown(new Point(80, 50), MouseButton.Left);
            rig.Viewport.Width = 0;
            rig.Window.UpdateLayout();

            rig.Window.MouseMove(new Point(85, 50));

            Assert.Empty(rig.Drags);
        });
    }

    [Fact]
    public void Another_button_does_not_grab_a_handle()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithHandles();

            rig.Window.MouseDown(new Point(80, 50), MouseButton.Right);
            rig.Window.MouseMove(new Point(90, 50));

            Assert.Empty(rig.Drags);
        });
    }

    [Fact]
    public void A_frame_that_is_scaled_to_fit_scales_the_move_back_to_frame_pixels()
    {
        UiThread.Run(() =>
        {
            // The same 100 by 50 frame in a 400 by 200 control is drawn at four times the size.
            var viewport = new ViewportControl(() => false, Frame) { HandlesSource = () => Handles, ModeSource = () => GizmoMode.Move };
            var drags = new List<Vector2>();
            viewport.GizmoDragged += drag => drags.Add(drag.Move.Delta);
            var window = new Window { Width = 400, Height = 200, Content = viewport };
            window.Show();
            viewport.Tick();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            window.MouseDown(new Point(160, 100), MouseButton.Left);
            window.MouseMove(new Point(180, 100));

            Assert.Equal([new Vector2(5, 0)], drags);
        });
    }

    static (EditorWindow Window, FakeScene Scene, ViewportControl Viewport) OpenWithGizmo()
    {
        var scene = new FakeScene { Handles = Handles };
        var window = OpenAtReferenceSize(scene);
        var viewport = new ViewportControl(() => false, Frame);
        window.ShowViewport(viewport);
        window.UpdateLayout();
        window.Outline.SelectedItem = FakeScene.Lamp;
        ClickOn(window.Toolbar.MoveButton, window);
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, scene, viewport);
    }

    static Point InWindow(Control viewport, Visual window, Vector2 framePoint)
    {
        var control = ViewportControl.ControlAt(framePoint, FrameSize, viewport.Bounds.Size);
        return viewport.TranslatePoint(new Point(control.X, control.Y), window)!.Value;
    }

    [Fact]
    public void The_viewport_asks_for_the_handles_of_a_selected_entity_3d()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            Assert.Contains(FakeScene.Lamp.Handle, scene.HandlesAsked);

            scene.HandlesAsked.Clear();
            window.Outline.SelectedItem = FakeScene.Sound;
            viewport.Tick();
            Assert.Empty(scene.HandlesAsked);

            window.Outline.SelectedItem = FakeScene.Lamp;
            viewport.Tick();
            Assert.Equal([FakeScene.Lamp.Handle], scene.HandlesAsked);

            window.ScenePane.Select(0);
            scene.HandlesAsked.Clear();
            viewport.Tick();
            Assert.Empty(scene.HandlesAsked);
        });
    }

    [Fact]
    public void The_local_button_and_the_T_key_flip_the_space_the_viewport_asks_in_and_the_drag_carries()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));
            Assert.Equal(GizmoSpace.Global, scene.HandleSpaces.Last());

            ClickOn(window.Toolbar.LocalButton, window);
            viewport.Tick();
            Assert.Equal(GizmoSpace.Local, scene.HandleSpaces.Last());
            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));
            window.MouseUp(onHandle + new Point(10, 0), MouseButton.Left);
            Assert.Equal(new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), scene.Dragged.Last().Direction);

            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.T });
            viewport.Tick();
            Assert.Equal(GizmoSpace.Global, scene.HandleSpaces.Last());
            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));
            Assert.Equal(new GizmoDirection(GizmoAxis.X, GizmoSpace.Global), scene.Dragged.Last().Direction);
        });
    }

    [Fact]
    public void Dragging_the_x_handle_moves_the_selected_entity_and_the_inspector_shows_it()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));

            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));

            var drag = Assert.Single(scene.Dragged);
            Assert.Equal(FakeScene.Lamp.Handle, drag.Entity);
            Assert.Equal(new GizmoDirection(GizmoAxis.X, GizmoSpace.Global), drag.Direction);
            Assert.True(drag.Pixels.X > 0);
            Assert.Equal(0f, drag.Pixels.Y);
            var x = scene.PositionOf(FakeScene.Lamp.Handle).X;
            Assert.Equal(1 + drag.Pixels.X, x, 3);
            Assert.Equal(x, float.Parse(window.Inspector.Position.Axes[0].Text!, CultureInfo.InvariantCulture), 2);
        });
    }

    [Fact]
    public void A_drag_marks_the_scene_edited_and_says_so()
    {
        UiThread.Run(() =>
        {
            var (window, _, viewport) = OpenWithGizmo();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));

            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));

            Assert.StartsWith("(*) ", window.Title);
            Assert.Equal("Moved Lamp. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void A_drag_keeps_the_entity_selected()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));

            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));
            window.MouseMove(onHandle + new Point(20, 0));

            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
            Assert.Equal(2, scene.Dragged.Count);
        });
    }

    [Fact]
    public void A_drag_redraws_the_selection_box_at_the_new_place()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            scene.Highlighted.Clear();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));

            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));

            Assert.Equal([FakeScene.Lamp.Handle], scene.Highlighted);
        });
    }

    [Fact]
    public void A_drag_after_the_selection_went_does_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));
            window.MouseDown(onHandle, MouseButton.Left);
            window.ScenePane.Select(0);
            var status = window.Status;

            window.MouseMove(onHandle + new Point(10, 0));

            Assert.Empty(scene.Dragged);
            Assert.Equal(status, window.Status);
        });
    }

    [Fact]
    public void Select_mode_shows_no_handles_and_the_other_modes_show_them_again()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();

            ClickOn(window.Toolbar.SelectButton, window);
            scene.HandlesAsked.Clear();
            viewport.Tick();
            Assert.Empty(scene.HandlesAsked);

            // A click draws a frame, which asks for the handles as well, so the count is not fixed.
            ClickOn(window.Toolbar.MoveButton, window);
            viewport.Tick();
            Assert.Contains(FakeScene.Lamp.Handle, scene.HandlesAsked);

            ClickOn(window.Toolbar.SelectButton, window);
            ClickOn(window.Toolbar.TransformButton, window);
            scene.HandlesAsked.Clear();
            viewport.Tick();
            Assert.Contains(FakeScene.Lamp.Handle, scene.HandlesAsked);
        });
    }

    [Fact]
    public void A_press_on_handles_before_select_mode_took_effect_moves_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));
            window.MouseDown(onHandle, MouseButton.Left);
            // A click on the button would release the pointer, so the key's route is used. Q presses it too.
            window.Toolbar.SelectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var status = window.Status;

            window.MouseMove(onHandle + new Point(10, 0));

            Assert.Empty(scene.Dragged);
            Assert.Equal(status, window.Status);
        });
    }

    [Fact]
    public void Scale_mode_asks_for_local_handles_and_a_drag_stretches_the_entity_and_does_not_move_it()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));

            ClickOn(window.Toolbar.ScaleButton, window);
            viewport.Tick();
            Assert.False(window.Toolbar.LocalButton.IsChecked);
            Assert.Equal(GizmoSpace.Local, scene.HandleSpaces.Last());
            scene.Highlighted.Clear();
            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));

            Assert.Empty(scene.Dragged);
            var drag = Assert.Single(scene.ScaleDragged);
            Assert.Equal(FakeScene.Lamp.Handle, drag.Entity);
            Assert.Equal(GizmoAxis.X, drag.Axis);
            Assert.True(drag.Pixels.X > 0);
            // The Lamp starts at a scale of 4, 5 and 6, so only the dragged axis changes.
            var scale = scene.ScaleOf(FakeScene.Lamp.Handle);
            var x = scale.X;
            Assert.Equal(4 * (1 + drag.Pixels.X * 0.01f), x, 3);
            Assert.Equal(new Vector2(5, 6), new Vector2(scale.Y, scale.Z));
            Assert.Equal(1f, scene.PositionOf(FakeScene.Lamp.Handle).X);
            Assert.Equal(x, float.Parse(window.Inspector.Scale.Axes[0].Text!, CultureInfo.InvariantCulture), 2);
            Assert.StartsWith("(*) ", window.Title);
            Assert.Equal("Scaled Lamp. Save keeps the change.", window.Status);
            Assert.Equal([FakeScene.Lamp.Handle], scene.Highlighted);
            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
        });
    }

    [Fact]
    public void Leaving_scale_mode_for_move_mode_goes_back_to_the_chosen_space_and_a_drag_that_moves()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithGizmo();
            var onHandle = InWindow(viewport, window, new Vector2(35, 25));
            ClickOn(window.Toolbar.ScaleButton, window);

            ClickOn(window.Toolbar.MoveButton, window);
            viewport.Tick();
            Assert.Equal(GizmoMode.Move, window.Toolbar.Mode);
            Assert.Equal(GizmoSpace.Global, scene.HandleSpaces.Last());
            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));

            Assert.Empty(scene.ScaleDragged);
            Assert.Single(scene.Dragged);
        });
    }
}
