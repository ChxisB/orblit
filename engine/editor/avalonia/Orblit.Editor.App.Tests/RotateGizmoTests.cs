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

public sealed class RotateGizmoTests
{
    // A 100 by 50 frame in a 200 by 100 control is drawn at twice the size, so a frame pixel is two control pixels.
    // The pivot is at (100, 50) in the control. The X ring is 40 control pixels wide, the Y ring 24 and the Z ring 12.
    static readonly Vector2 Pivot = new(50, 25);

    static readonly GizmoRings Rings = new(Pivot, Circle(20), Circle(12), Circle(6));

    static readonly Size FrameSize = new(100, 50);

    static Vector2[] Circle(float radius) =>
    [
        .. Enumerable.Range(0, GizmoMath.RingPoints).Select(i =>
            Pivot + radius * new Vector2(MathF.Cos(MathF.Tau * i / GizmoMath.RingPoints), MathF.Sin(MathF.Tau * i / GizmoMath.RingPoints))),
    ];

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

        public List<(GizmoAxis Axis, float Radians)> Turns { get; } = [];

        public List<Vector2> Clicks { get; } = [];

        public List<(float Yaw, float Pitch)> Orbits { get; } = [];
    }

    static Rig Shown(Func<GizmoRings?>? source, double width = 200, double height = 100)
    {
        var viewport = new ViewportControl(() => false, Frame) { RingsSource = source, ModeSource = () => GizmoMode.Rotate };
        var rig = new Rig { Window = new Window { Width = width, Height = height, Content = viewport }, Viewport = viewport };
        viewport.GizmoDragged += drag => rig.Turns.Add((drag.Hit.Axis, GizmoMath.RingAngle(Rings.Of(drag.Hit.Axis), drag.Move.To, drag.Move.Delta)));
        viewport.Clicked += rig.Clicks.Add;
        viewport.Orbited += (yaw, pitch) => rig.Orbits.Add((yaw, pitch));
        rig.Window.Show();
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return rig;
    }

    static Rig ShownWithRings() => Shown(() => Rings);

    [Fact]
    public void Each_axis_has_its_own_ring()
    {
        var rings = new GizmoRings(Vector2.Zero, [Vector2.One], [Vector2.UnitX], [Vector2.UnitY]);

        Assert.Equal([Vector2.One], rings.Of(GizmoAxis.X));
        Assert.Equal([Vector2.UnitX], rings.Of(GizmoAxis.Y));
        Assert.Equal([Vector2.UnitY], rings.Of(GizmoAxis.Z));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(1, 1, 1)]
    public void A_ring_is_48_points_of_a_circle_across_its_axis(float x, float y, float z)
    {
        var axis = Vector3.Normalize(new Vector3(x, y, z));
        var center = new Vector3(1, 2, 3);

        var ring = GizmoMath.Ring(center, axis, 5);

        Assert.Equal(48, ring.Length);
        Assert.All(ring, point => Assert.Equal(5f, Vector3.Distance(point, center), 3));
        Assert.All(ring, point => Assert.Equal(0f, Vector3.Dot(point - center, axis), 3));
        Assert.Equal(48, ring.Select(point => point.ToString()).Distinct().Count());
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(1, 1, 1)]
    public void A_ring_runs_the_way_a_turn_about_its_axis_takes_a_point(float x, float y, float z)
    {
        var axis = Vector3.Normalize(new Vector3(x, y, z));
        var ring = GizmoMath.Ring(Vector3.Zero, axis, 5);

        var turned = Vector3.Transform(ring[0], Quaternion.CreateFromAxisAngle(axis, MathF.Tau / GizmoMath.RingPoints));

        Assert.Equal(0f, Vector3.Distance(ring[1], turned), 3);
    }

    [Fact]
    public void A_drag_along_the_ring_turns_by_its_share_of_the_handle_length()
    {
        var ring = Circle(20);
        // At the start of the ring the screen direction is down, the way the ring runs.
        Assert.Equal(0.5f, GizmoMath.RingAngle(ring, new Vector2(70, 25), new Vector2(0, 40)), 4);
        Assert.Equal(-0.1f, GizmoMath.RingAngle(ring, new Vector2(70, 25), new Vector2(0, -8)), 4);
        Assert.Equal(0f, GizmoMath.RingAngle(ring, new Vector2(70, 25), new Vector2(40, 0)), 4);
        // A quarter of the way round it runs left.
        Assert.Equal(0.5f, GizmoMath.RingAngle(ring, new Vector2(50, 45), new Vector2(-40, 0)), 4);
    }

    [Fact]
    public void Many_small_drags_turn_as_far_as_one_big_one()
    {
        var ring = Circle(20);
        var steps = Enumerable.Range(0, 4).Sum(_ => GizmoMath.RingAngle(ring, new Vector2(70, 25), new Vector2(0, 10)));

        Assert.Equal(GizmoMath.RingAngle(ring, new Vector2(70, 25), new Vector2(0, 40)), steps, 4);
    }

    [Fact]
    public void A_ring_that_shows_as_a_point_turns_nothing()
    {
        var ring = Enumerable.Repeat(Pivot, GizmoMath.RingPoints).ToArray();

        Assert.Equal(0f, GizmoMath.RingAngle(ring, Pivot, new Vector2(30, 30)));
    }

    [Fact]
    public void A_turn_keeps_the_place_and_every_column_length_and_turns_the_axes()
    {
        var place = new Transform3D(new Vector3(2, 0, 0), new Vector3(0, 3, 0), new Vector3(0, 0, 4), new Vector3(7, 8, 9));

        var turned = GizmoMath.Turned(place, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));

        // A quarter turn about Y takes X to minus Z and Z to X.
        Assert.Equal(0f, Vector3.Distance(new Vector3(0, 0, 4), turned.Row0), 4);
        Assert.Equal(0f, Vector3.Distance(new Vector3(0, 3, 0), turned.Row1), 4);
        Assert.Equal(0f, Vector3.Distance(new Vector3(-2, 0, 0), turned.Row2), 4);
        Assert.Equal(new Vector3(7, 8, 9), turned.Origin);
    }

    [Fact]
    public void A_turn_acts_on_the_columns_of_the_basis_and_not_its_rows()
    {
        // The columns are (0,1,0), (0,0,2) and (3,0,0), so every row differs from its column.
        var place = new Transform3D(new Vector3(0, 0, 3), new Vector3(1, 0, 0), new Vector3(0, 2, 0), Vector3.Zero);

        var turned = GizmoMath.Turned(place, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2));

        // A quarter turn about Z takes X to Y and Y to minus X, so the columns become (-1,0,0), (0,0,2) and (0,3,0).
        Assert.Equal(0f, Vector3.Distance(new Vector3(-1, 0, 0), turned.Row0), 4);
        Assert.Equal(0f, Vector3.Distance(new Vector3(0, 0, 3), turned.Row1), 4);
        Assert.Equal(0f, Vector3.Distance(new Vector3(0, 2, 0), turned.Row2), 4);
    }

    [Fact]
    public void A_turn_about_a_slanted_axis_keeps_every_column_length()
    {
        // No row has the length of the column with its number, so a turn that acts on rows is caught.
        var place = new Transform3D(new Vector3(1, 3, 0), new Vector3(0, 3, 1), new Vector3(2, 0, 4), Vector3.Zero);
        var turned = GizmoMath.Turned(place, Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1, 2, 3)), 1.1f));

        float Length(Transform3D t, int column) => new Vector3(Column(t.Row0, column), Column(t.Row1, column), Column(t.Row2, column)).Length();
        static float Column(Vector3 row, int column) => column switch { 0 => row.X, 1 => row.Y, _ => row.Z };

        foreach (var column in new[] { 0, 1, 2 })
        {
            Assert.Equal(Length(place, column), Length(turned, column), 4);
        }
    }

    [Theory]
    [InlineData(140, 50, GizmoAxis.X)]
    [InlineData(100, 74, GizmoAxis.Y)]
    [InlineData(112, 50, GizmoAxis.Z)]
    [InlineData(146, 50, GizmoAxis.X)]
    public void A_press_on_a_ring_takes_it(double x, double y, GizmoAxis axis)
    {
        UiThread.Run(() => Assert.Equal(axis, ShownWithRings().Viewport.RingAt(new Point(x, y))));
    }

    [Theory]
    [InlineData(150, 50)]
    [InlineData(100, 50)]
    [InlineData(109, 50)]
    [InlineData(100, 0)]
    [InlineData(10, 90)]
    public void A_press_off_the_rings_or_on_the_pivot_takes_none(double x, double y)
    {
        UiThread.Run(() => Assert.Null(ShownWithRings().Viewport.RingAt(new Point(x, y))));
    }

    [Fact]
    public void A_press_takes_nothing_when_there_are_no_rings()
    {
        UiThread.Run(() =>
        {
            Assert.Null(Shown(null).Viewport.RingAt(new Point(140, 50)));
            Assert.Null(Shown(() => null).Viewport.RingAt(new Point(140, 50)));
        });
    }

    [Fact]
    public void The_nearest_ring_takes_a_press_between_two()
    {
        UiThread.Run(() =>
        {
            // The X ring is 40 control pixels from the pivot and the Y ring 28, so 33 is 7 from X and 5 from Y.
            var viewport = Shown(() => new GizmoRings(Pivot, Circle(20), Circle(14), Circle(1))).Viewport;
            Assert.Equal(GizmoAxis.Y, viewport.RingAt(new Point(133, 50)));
            Assert.Equal(GizmoAxis.X, viewport.RingAt(new Point(135, 50)));
        });
    }

    [Fact]
    public void The_source_is_asked_on_each_tick()
    {
        UiThread.Run(() =>
        {
            var asked = 0;
            var rig = Shown(() =>
            {
                asked++;
                return Rings;
            });
            var before = asked;

            rig.Viewport.Tick();
            rig.Viewport.Tick();

            Assert.Equal(before + 2, asked);
        });
    }

    [Fact]
    public void A_drag_that_starts_on_a_ring_raises_the_turn_and_does_not_orbit_or_click()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithRings();

            rig.Window.MouseDown(new Point(140, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(140, 60));
            rig.Window.MouseMove(new Point(140, 70));
            rig.Window.MouseUp(new Point(140, 70), MouseButton.Left);

            // 10 control pixels are 5 frame pixels, and the ring runs down there, so about 5 over 80 each time.
            Assert.Equal(2, rig.Turns.Count);
            Assert.All(rig.Turns, turn => Assert.Equal(GizmoAxis.X, turn.Axis));
            Assert.All(rig.Turns, turn => Assert.InRange(turn.Radians, 0.05f, 0.0625f));
            Assert.Empty(rig.Orbits);
            Assert.Empty(rig.Clicks);
        });
    }

    [Fact]
    public void A_drag_against_the_way_a_ring_runs_turns_back()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithRings();

            rig.Window.MouseDown(new Point(140, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(140, 40));

            Assert.InRange(Assert.Single(rig.Turns).Radians, -0.0625f, -0.05f);
        });
    }

    [Fact]
    public void A_ring_turns_from_the_first_pixel()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithRings();

            rig.Window.MouseDown(new Point(100, 74), MouseButton.Left);
            rig.Window.MouseMove(new Point(98, 74));

            Assert.Equal([GizmoAxis.Y], rig.Turns.Select(turn => turn.Axis));
        });
    }

    [Fact]
    public void A_frame_that_is_scaled_to_fit_scales_the_turn_back_to_frame_pixels()
    {
        UiThread.Run(() =>
        {
            // The same frame in a 400 by 200 control is drawn at four times the size, so 20 control pixels are 5 frame pixels.
            var rig = Shown(() => Rings, 400, 200);

            rig.Window.MouseDown(new Point(280, 100), MouseButton.Left);
            rig.Window.MouseMove(new Point(280, 120));

            Assert.InRange(Assert.Single(rig.Turns).Radians, 0.05f, 0.0625f);
        });
    }

    [Fact]
    public void A_frame_with_bars_beside_it_turns_by_the_ring_under_the_pointer()
    {
        UiThread.Run(() =>
        {
            // In a 300 by 100 control the frame is drawn at twice the size with a 50 pixel bar to its left.
            // The press is on the X ring a quarter of the way round, where it runs left.
            var rig = Shown(() => Rings, 300, 100);

            rig.Window.MouseDown(new Point(150, 90), MouseButton.Left);
            rig.Window.MouseMove(new Point(140, 90));

            Assert.InRange(Assert.Single(rig.Turns).Radians, 0.05f, 0.0625f);
        });
    }

    [Fact]
    public void A_press_on_the_pivot_still_clicks_and_orbits_and_turns_nothing()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithRings();

            rig.Window.MouseDown(new Point(100, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(102, 50));
            rig.Window.MouseMove(new Point(120, 50));
            rig.Window.MouseUp(new Point(120, 50), MouseButton.Left);

            Assert.Empty(rig.Turns);
            Assert.NotEmpty(rig.Orbits);
        });
    }

    [Fact]
    public void A_press_in_empty_space_orbits_while_rings_show()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithRings();

            rig.Window.MouseDown(new Point(20, 90), MouseButton.Left);
            rig.Window.MouseMove(new Point(40, 90));

            Assert.Empty(rig.Turns);
            Assert.Single(rig.Orbits);
        });
    }

    [Fact]
    public void Letting_go_of_a_ring_ends_the_grab()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithRings();
            rig.Window.MouseDown(new Point(140, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(140, 60));
            rig.Window.MouseUp(new Point(140, 60), MouseButton.Left);

            rig.Window.MouseMove(new Point(140, 70));

            Assert.Single(rig.Turns);
            Assert.Empty(rig.Orbits);
        });
    }

    [Fact]
    public void Another_button_does_not_grab_a_ring()
    {
        UiThread.Run(() =>
        {
            var rig = ShownWithRings();

            rig.Window.MouseDown(new Point(140, 50), MouseButton.Right);
            rig.Window.MouseMove(new Point(140, 60));

            Assert.Empty(rig.Turns);
        });
    }

    static (EditorWindow Window, FakeScene Scene, ViewportControl Viewport) OpenWithRings()
    {
        var scene = new FakeScene { Rings = Rings, Handles = new GizmoHandles(Pivot, Pivot + new Vector2(30, 0), Pivot, Pivot) };
        var window = OpenAtReferenceSize(scene);
        var viewport = new ViewportControl(() => false, Frame);
        window.ShowViewport(viewport);
        window.UpdateLayout();
        window.Outline.SelectedItem = FakeScene.Lamp;
        ClickOn(window.Toolbar.RotateButton, window);
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, scene, viewport);
    }

    // The window stops asking for rings as soon as the mode or the selection moves on, and a frame that ticks the
    // viewport then drops them. A move that lands before that frame still has them, and this keeps them to say so.
    static void KeepRings(ViewportControl viewport)
    {
        viewport.RingsSource = () => Rings;
    }

    static Point InWindow(Control viewport, Visual window, Vector2 framePoint)
    {
        var control = ViewportControl.ControlAt(framePoint, FrameSize, viewport.Bounds.Size);
        return viewport.TranslatePoint(new Point(control.X, control.Y), window)!.Value;
    }

    [Fact]
    public void Rotate_mode_asks_for_rings_and_no_handles_and_the_other_modes_ask_for_handles_and_no_rings()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithRings();
            scene.HandlesAsked.Clear();
            scene.RingsAsked.Clear();
            viewport.Tick();
            Assert.Equal([FakeScene.Lamp.Handle], scene.RingsAsked);
            Assert.Empty(scene.HandlesAsked);

            ClickOn(window.Toolbar.MoveButton, window);
            scene.RingsAsked.Clear();
            viewport.Tick();
            Assert.Empty(scene.RingsAsked);
            Assert.Contains(FakeScene.Lamp.Handle, scene.HandlesAsked);

            ClickOn(window.Toolbar.RotateButton, window);
            scene.HandlesAsked.Clear();
            viewport.Tick();
            Assert.Empty(scene.HandlesAsked);
            Assert.Equal([FakeScene.Lamp.Handle], scene.RingsAsked.TakeLast(1));
        });
    }

    [Fact]
    public void Rotate_mode_asks_for_no_rings_without_an_entity_3d()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithRings();

            window.Outline.SelectedItem = FakeScene.Sound;
            scene.RingsAsked.Clear();
            viewport.Tick();

            Assert.Empty(scene.RingsAsked);
        });
    }

    [Fact]
    public void Dragging_a_ring_turns_the_selected_entity_and_the_inspector_shows_it()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithRings();
            var onRing = InWindow(viewport, window, new Vector2(70, 25));
            scene.Highlighted.Clear();

            window.MouseDown(onRing, MouseButton.Left);
            window.MouseMove(onRing + new Point(0, 10));

            var drag = Assert.Single(scene.RotateDragged);
            Assert.Equal(FakeScene.Lamp.Handle, drag.Entity);
            Assert.Equal(new GizmoDirection(GizmoAxis.X, GizmoSpace.Global), drag.Direction);
            Assert.True(drag.Radians > 0);
            // The Lamp starts turned 10, 20 and 30 degrees, so only the dragged axis changes.
            var turned = scene.RotationOf(FakeScene.Lamp.Handle);
            Assert.Equal(10 + drag.Radians * 180 / MathF.PI, turned.X, 2);
            Assert.Equal(new Vector2(20, 30), new Vector2(turned.Y, turned.Z));
            Assert.Equal(turned.X, float.Parse(window.Inspector.Rotation.Axes[0].Text!, CultureInfo.InvariantCulture), 1);
            Assert.Equal(new Vector3(1, 2, 3), scene.PositionOf(FakeScene.Lamp.Handle));
            Assert.Empty(scene.Dragged);
            Assert.Empty(scene.ScaleDragged);
            Assert.StartsWith("(*) ", window.Title);
            Assert.Equal("Rotated Lamp. Save keeps the change.", window.Status);
            Assert.Equal([FakeScene.Lamp.Handle], scene.Highlighted);
            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
        });
    }

    [Fact]
    public void The_local_button_flips_the_space_the_rings_are_asked_in_and_the_turn_carries()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithRings();
            var onRing = InWindow(viewport, window, new Vector2(70, 25));
            Assert.Equal(GizmoSpace.Global, scene.RingSpaces.Last());

            ClickOn(window.Toolbar.LocalButton, window);
            viewport.Tick();
            Assert.Equal(GizmoSpace.Local, scene.RingSpaces.Last());
            window.MouseDown(onRing, MouseButton.Left);
            window.MouseMove(onRing + new Point(0, 10));
            window.MouseUp(onRing + new Point(0, 10), MouseButton.Left);
            Assert.Equal(new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), scene.RotateDragged.Last().Direction);

            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.T });
            viewport.Tick();
            Assert.Equal(GizmoSpace.Global, scene.RingSpaces.Last());
        });
    }

    [Fact]
    public void Leaving_rotate_mode_for_move_mode_makes_a_drag_that_moves()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithRings();
            ClickOn(window.Toolbar.MoveButton, window);
            viewport.Tick();
            var onHandle = InWindow(viewport, window, Pivot + new Vector2(15, 0));

            window.MouseDown(onHandle, MouseButton.Left);
            window.MouseMove(onHandle + new Point(10, 0));

            Assert.Empty(scene.RotateDragged);
            Assert.Single(scene.Dragged);
        });
    }

    [Fact]
    public void A_press_on_rings_before_select_mode_took_effect_turns_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithRings();
            var onRing = InWindow(viewport, window, new Vector2(70, 25));
            window.MouseDown(onRing, MouseButton.Left);
            // A click on the button would release the pointer, so the event is raised on it.
            window.Toolbar.SelectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            KeepRings(viewport);
            var status = window.Status;

            window.MouseMove(onRing + new Point(0, 10));

            Assert.Empty(scene.RotateDragged);
            Assert.Equal(status, window.Status);
        });
    }

    [Fact]
    public void A_press_on_rings_before_move_mode_took_effect_turns_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithRings();
            var onRing = InWindow(viewport, window, new Vector2(70, 25));
            window.MouseDown(onRing, MouseButton.Left);
            window.Toolbar.MoveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            KeepRings(viewport);

            window.MouseMove(onRing + new Point(0, 10));

            Assert.Empty(scene.RotateDragged);
        });
    }

    [Fact]
    public void A_turn_after_the_selection_went_does_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenWithRings();
            var onRing = InWindow(viewport, window, new Vector2(70, 25));
            window.MouseDown(onRing, MouseButton.Left);
            window.ScenePane.Select(0);
            KeepRings(viewport);

            window.MouseMove(onRing + new Point(0, 10));

            Assert.Empty(scene.RotateDragged);
        });
    }
}
