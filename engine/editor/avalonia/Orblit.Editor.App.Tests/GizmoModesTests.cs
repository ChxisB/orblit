using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;
using Vector2 = System.Numerics.Vector2;

namespace Orblit.Editor.App.Tests;

public sealed class GizmoModesTests
{
    // Hit zones, in control pixels. The pivot is at (100, 50). The X handle runs right to (160, 50) and the Y handle
    // up to (100, 10). The Z axis points at the camera and shows as a dot. The rings are circles of radius 28, 24 and
    // 20 for X, Y and Z, and the outer ring is a circle of radius 60.
    static readonly Vector2 Centre = new(100, 50);

    static readonly GizmoHandles Handles = new(Centre, new Vector2(160, 50), new Vector2(100, 10), Centre);

    static readonly GizmoRings Rings = new(Centre, Circle(Centre, 28), Circle(Centre, 24), Circle(Centre, 20)) { Outer = Circle(Centre, 60) };

    static readonly GizmoRings RingsWithoutOuter = new(Centre, Circle(Centre, 28), Circle(Centre, 24), Circle(Centre, 20));

    static Vector2[] Circle(Vector2 centre, float radius) =>
    [
        .. Enumerable.Range(0, GizmoMath.RingPoints).Select(i =>
            centre + radius * new Vector2(MathF.Cos(MathF.Tau * i / GizmoMath.RingPoints), MathF.Sin(MathF.Tau * i / GizmoMath.RingPoints))),
    ];

    static GizmoHit? Hit(GizmoMode mode, Vector2 at, bool trackball = false, GizmoRings? rings = null) =>
        GizmoZones.HitAt(new GizmoShown(mode, trackball, Handles, rings ?? Rings), at);

    [Fact]
    public void Transform_stretches_from_a_tip_moves_from_a_shaft_and_turns_from_a_ring()
    {
        Assert.Equal(new GizmoHit(GizmoPart.Stretch, GizmoAxis.X), Hit(GizmoMode.Transform, new Vector2(160, 50)));
        Assert.Equal(new GizmoHit(GizmoPart.Stretch, GizmoAxis.Y), Hit(GizmoMode.Transform, new Vector2(100, 12)));
        Assert.Equal(new GizmoHit(GizmoPart.Move, GizmoAxis.X), Hit(GizmoMode.Transform, new Vector2(125, 50)));
        Assert.Equal(new GizmoHit(GizmoPart.Turn, GizmoAxis.X), Hit(GizmoMode.Transform, new Vector2(100, 78)));
    }

    [Fact]
    public void Transform_has_no_centre_handle_and_no_outer_ring()
    {
        Assert.Null(Hit(GizmoMode.Transform, new Vector2(103, 52)));
        Assert.Null(Hit(GizmoMode.Transform, new Vector2(100, 110)));
    }

    [Fact]
    public void Move_takes_a_shaft_all_the_way_to_its_tip_and_nothing_else()
    {
        Assert.Equal(new GizmoHit(GizmoPart.Move, GizmoAxis.X), Hit(GizmoMode.Move, new Vector2(160, 50)));
        Assert.Equal(new GizmoHit(GizmoPart.Move, GizmoAxis.X), Hit(GizmoMode.Move, new Vector2(125, 50)));
        Assert.Null(Hit(GizmoMode.Move, new Vector2(169, 50)));
        Assert.Null(Hit(GizmoMode.Move, new Vector2(100, 78)));
        Assert.Null(Hit(GizmoMode.Move, new Vector2(103, 52)));
    }

    [Fact]
    public void Scale_takes_a_shaft_for_one_axis_and_the_centre_for_all_three()
    {
        Assert.Equal(new GizmoHit(GizmoPart.Stretch, GizmoAxis.X), Hit(GizmoMode.Scale, new Vector2(130, 50)));
        Assert.Equal(new GizmoHit(GizmoPart.StretchAll, GizmoAxis.X), Hit(GizmoMode.Scale, new Vector2(103, 52)));
        Assert.Null(Hit(GizmoMode.Scale, new Vector2(100, 78)));
    }

    [Fact]
    public void Rotate_takes_a_ring_first_then_the_outer_ring_and_then_the_ball_when_there_is_one()
    {
        Assert.Equal(new GizmoHit(GizmoPart.Turn, GizmoAxis.X), Hit(GizmoMode.Rotate, new Vector2(100, 78)));
        Assert.Equal(new GizmoHit(GizmoPart.Spin, GizmoAxis.X), Hit(GizmoMode.Rotate, new Vector2(160, 50)));
        Assert.Null(Hit(GizmoMode.Rotate, new Vector2(144, 50)));
        Assert.Equal(new GizmoHit(GizmoPart.Trackball, GizmoAxis.X), Hit(GizmoMode.Rotate, new Vector2(144, 50), trackball: true));
    }

    [Fact]
    public void The_ball_is_inside_the_outer_ring_only_and_never_on_the_pivot()
    {
        Assert.Null(Hit(GizmoMode.Rotate, new Vector2(100, 150), trackball: true));
        Assert.Null(Hit(GizmoMode.Rotate, new Vector2(104, 50), trackball: true));
    }

    [Fact]
    public void Rings_with_no_outer_ring_offer_no_spin_and_no_ball()
    {
        Assert.Null(Hit(GizmoMode.Rotate, new Vector2(160, 50), trackball: true, rings: RingsWithoutOuter));
        Assert.Null(Hit(GizmoMode.Rotate, new Vector2(144, 50), trackball: true, rings: RingsWithoutOuter));
        Assert.Equal(new GizmoHit(GizmoPart.Turn, GizmoAxis.X), Hit(GizmoMode.Rotate, new Vector2(100, 78), rings: RingsWithoutOuter));
    }

    [Fact]
    public void Where_parts_overlap_a_tip_wins_over_a_shaft_and_a_shaft_over_a_ring()
    {
        // The X ring passes through the X tip and the Z ring through the shaft of X.
        var rings = new GizmoRings(Centre, Circle(Centre, 60), Circle(Centre, 24), Circle(Centre, 30));

        Assert.Equal(new GizmoHit(GizmoPart.Stretch, GizmoAxis.X), Hit(GizmoMode.Transform, new Vector2(160, 50), rings: rings));
        Assert.Equal(new GizmoHit(GizmoPart.Move, GizmoAxis.X), Hit(GizmoMode.Transform, new Vector2(130, 50), rings: rings));
        Assert.Equal(new GizmoHit(GizmoPart.Turn, GizmoAxis.Z), Hit(GizmoMode.Transform, new Vector2(100, 80), rings: rings));
    }

    [Fact]
    public void Where_the_outer_ring_meets_an_axis_ring_the_axis_ring_wins()
    {
        var rings = new GizmoRings(Centre, Circle(Centre, 28), Circle(Centre, 24), Circle(Centre, 20)) { Outer = Circle(Centre, 28) };

        Assert.Equal(new GizmoHit(GizmoPart.Turn, GizmoAxis.X), Hit(GizmoMode.Rotate, new Vector2(128, 50), rings: rings));
    }

    [Fact]
    public void A_press_near_the_pivot_takes_no_ring_even_when_the_outer_ring_is_small()
    {
        var rings = new GizmoRings(Centre, Circle(Centre, 3), Circle(Centre, 3), Circle(Centre, 3)) { Outer = Circle(Centre, 12) };

        Assert.Null(Hit(GizmoMode.Rotate, new Vector2(105, 50), rings: rings));
        Assert.Equal(new GizmoHit(GizmoPart.Spin, GizmoAxis.X), Hit(GizmoMode.Rotate, new Vector2(112, 50), rings: rings));
    }

    [Fact]
    public void Select_takes_nothing_and_a_missing_gizmo_takes_nothing()
    {
        Assert.Null(Hit(GizmoMode.Select, new Vector2(160, 50)));
        Assert.Null(GizmoZones.HitAt(new GizmoShown(GizmoMode.Transform, false, null, null), new Vector2(160, 50)));
        Assert.Null(GizmoZones.HitAt(new GizmoShown(GizmoMode.Rotate, true, null, null), new Vector2(160, 50)));
    }

    [Theory]
    [InlineData("Transform", true, true)]
    [InlineData("Move", true, false)]
    [InlineData("Scale", true, false)]
    [InlineData("Rotate", false, true)]
    [InlineData("Select", false, false)]
    public void Each_mode_says_whether_it_shows_handles_and_rings(string name, bool handles, bool rings)
    {
        var mode = Enum.Parse<GizmoMode>(name);

        Assert.Equal(handles, GizmoZones.HasHandles(mode));
        Assert.Equal(rings, GizmoZones.HasRings(mode));
    }

    [Fact]
    public void Each_mode_offers_only_the_parts_it_takes_a_press_on()
    {
        foreach (var mode in Enum.GetValues<GizmoMode>())
        {
            foreach (var part in Enum.GetValues<GizmoPart>())
            {
                var taken = new[] { new Vector2(160, 50), new Vector2(125, 50), new Vector2(130, 50), new Vector2(103, 52), new Vector2(100, 78), new Vector2(144, 50) }
                    .Any(at => Hit(mode, at, trackball: true) is { } hit && hit.Part == part);
                Assert.True(!taken || GizmoZones.Offers(mode, part), $"{mode} takes {part} but does not offer it");
            }
        }

        Assert.False(GizmoZones.Offers(GizmoMode.Move, GizmoPart.Stretch));
        Assert.False(GizmoZones.Offers(GizmoMode.Scale, GizmoPart.Move));
        Assert.False(GizmoZones.Offers(GizmoMode.Select, GizmoPart.Turn));
    }

    // A window with a viewport of the same shape as the Move and Rotate tests: a 100 by 50 frame. The pivot is at
    // (50, 25) in it, the X handle runs right to (80, 25), the X ring has radius 14 and the outer ring radius 30.
    static readonly Vector2 Pivot = new(50, 25);

    static readonly Size FrameSize = new(100, 50);

    static bool Frame(out int width, out int height, ref byte[] rgba)
    {
        width = 100;
        height = 50;
        rgba = new byte[width * height * 4];
        return true;
    }

    static (EditorWindow Window, FakeScene Scene, ViewportControl Viewport) Open(Func<EditorWindow, Control> mode)
    {
        var scene = new FakeScene
        {
            Handles = new GizmoHandles(Pivot, new Vector2(80, 25), new Vector2(50, 0), Pivot),
            Rings = new GizmoRings(Pivot, Circle(Pivot, 14), Circle(Pivot, 12), Circle(Pivot, 10)) { Outer = Circle(Pivot, 30) },
        };
        var window = OpenAtReferenceSize(scene);
        var viewport = new ViewportControl(() => false, Frame);
        window.ShowViewport(viewport);
        window.UpdateLayout();
        window.Outline.SelectedItem = FakeScene.Lamp;
        ClickOn(mode(window), window);
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, scene, viewport);
    }

    static Point InWindow(Control viewport, Visual window, Vector2 framePoint)
    {
        var control = ViewportControl.ControlAt(framePoint, FrameSize, viewport.Bounds.Size);
        return viewport.TranslatePoint(new Point(control.X, control.Y), window)!.Value;
    }

    static void Drag(EditorWindow window, ViewportControl viewport, Vector2 framePoint, RawInputModifiers keys = RawInputModifiers.None)
    {
        var at = InWindow(viewport, window, framePoint);
        window.MouseDown(at, MouseButton.Left, keys);
        window.MouseMove(at + new Point(10, 0), keys);
        window.MouseUp(at + new Point(10, 0), MouseButton.Left, keys);
    }

    [Fact]
    public void In_scale_mode_a_press_on_the_centre_stretches_all_three_axes()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = Open(w => w.Toolbar.ScaleButton);

            Drag(window, viewport, Pivot);

            var drag = Assert.Single(scene.ScaleAllDragged);
            Assert.Equal(FakeScene.Lamp.Handle, drag.Entity);
            Assert.True(drag.Pixels.X > 0);
            Assert.Empty(scene.ScaleDragged);
            Assert.StartsWith("Scaled ", window.Status);
        });
    }

    [Fact]
    public void In_scale_mode_a_press_on_a_shaft_stretches_one_axis()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = Open(w => w.Toolbar.ScaleButton);

            Drag(window, viewport, new Vector2(65, 25));

            Assert.Equal(GizmoAxis.X, Assert.Single(scene.ScaleDragged).Axis);
            Assert.Empty(scene.ScaleAllDragged);
        });
    }

    [Fact]
    public void In_transform_mode_a_tip_stretches_a_shaft_moves_and_a_ring_turns_and_the_axes_are_the_entitys_own()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = Open(w => w.Toolbar.TransformButton);

            Drag(window, viewport, new Vector2(80, 25));
            Assert.Equal(GizmoAxis.X, Assert.Single(scene.ScaleDragged).Axis);
            Assert.StartsWith("Scaled ", window.Status);

            Drag(window, viewport, new Vector2(65, 25));
            var move = Assert.Single(scene.Dragged);
            Assert.Equal(new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), move.Direction);
            Assert.StartsWith("Moved ", window.Status);

            Drag(window, viewport, new Vector2(50, 39));
            var turn = Assert.Single(scene.RotateDragged);
            Assert.Equal(new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), turn.Direction);
            Assert.StartsWith("Rotated ", window.Status);
        });
    }

    [Fact]
    public void Transform_mode_asks_for_handles_and_rings_and_move_mode_for_handles_alone()
    {
        UiThread.Run(() =>
        {
            var (_, scene, viewport) = Open(w => w.Toolbar.TransformButton);
            scene.HandlesAsked.Clear();
            scene.RingsAsked.Clear();
            viewport.Tick();
            Assert.NotEmpty(scene.HandlesAsked);
            Assert.NotEmpty(scene.RingsAsked);

            var (_, moveScene, moveViewport) = Open(w => w.Toolbar.MoveButton);
            moveScene.HandlesAsked.Clear();
            moveScene.RingsAsked.Clear();
            moveViewport.Tick();
            Assert.NotEmpty(moveScene.HandlesAsked);
            Assert.Empty(moveScene.RingsAsked);
        });
    }

    [Fact]
    public void In_rotate_mode_the_outer_ring_spins_and_the_ball_rolls_only_when_the_trackball_is_on()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = Open(w => w.Toolbar.RotateButton);

            Drag(window, viewport, new Vector2(80, 25));
            Assert.Single(scene.SpinDragged);
            Assert.StartsWith("Rotated ", window.Status);

            Drag(window, viewport, new Vector2(70, 25));
            Assert.Empty(scene.TrackballDragged);

            ClickOn(window.Toolbar.TrackballButton, window);
            Drag(window, viewport, new Vector2(70, 25));
            Assert.Single(scene.TrackballDragged);
            Assert.Single(scene.SpinDragged);
        });
    }

    [Theory]
    [InlineData(false, RawInputModifiers.None, false)]
    [InlineData(true, RawInputModifiers.None, true)]
    [InlineData(false, RawInputModifiers.Control, true)]
    [InlineData(false, RawInputModifiers.Meta, true)]
    [InlineData(true, RawInputModifiers.Control, false)]
    [InlineData(true, RawInputModifiers.Meta, false)]
    [InlineData(false, RawInputModifiers.Shift, false)]
    [InlineData(true, RawInputModifiers.Shift, true)]
    [InlineData(false, RawInputModifiers.Alt, false)]
    public void The_snap_button_and_the_snap_key_choose_whether_a_drag_snaps(bool lit, RawInputModifiers keys, bool snaps)
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = Open(w => w.Toolbar.MoveButton);
            window.Toolbar.SnapMoveBox.Text = "2";
            window.Toolbar.SnapTurnBox.Text = "30";
            window.Toolbar.SnapScaleBox.Text = "0.5";
            if (lit)
            {
                ClickOn(window.Toolbar.SnapButton, window);
            }

            Drag(window, viewport, new Vector2(65, 25), keys);

            Assert.Equal(snaps ? new GizmoSnap(2f, 30f, 0.5f) : null, scene.SnapsSeen.Single());
        });
    }

    [Fact]
    public void A_release_ends_the_drag_and_a_press_that_grabbed_nothing_does_not()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = Open(w => w.Toolbar.MoveButton);

            Drag(window, viewport, new Vector2(65, 25));
            Assert.Equal(1, scene.DragsEnded);

            Drag(window, viewport, new Vector2(10, 40));
            Assert.Equal(1, scene.DragsEnded);

            Drag(window, viewport, new Vector2(65, 25));
            Assert.Equal(2, scene.DragsEnded);
        });
    }

    [Fact]
    public void A_drag_that_began_in_another_mode_does_nothing_once_the_mode_has_changed()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = Open(w => w.Toolbar.MoveButton);
            var at = InWindow(viewport, window, new Vector2(65, 25));

            window.MouseDown(at, MouseButton.Left);
            window.Toolbar.ScaleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.MouseMove(at + new Point(10, 0));

            Assert.Empty(scene.Dragged);
            Assert.Empty(scene.ScaleDragged);
            Assert.Empty(scene.ScaleAllDragged);
        });
    }

    [Fact]
    public void Select_mode_has_no_gizmo_to_grab()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = Open(w => w.Toolbar.SelectButton);
            scene.HandlesAsked.Clear();
            scene.RingsAsked.Clear();
            viewport.Tick();

            Drag(window, viewport, new Vector2(65, 25));

            Assert.Empty(scene.HandlesAsked);
            Assert.Empty(scene.RingsAsked);
            Assert.Empty(scene.Dragged);
        });
    }

    [Fact]
    public void The_toolbar_reports_the_mode_that_is_lit()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var toolbar = window.Toolbar;
            Assert.Equal(GizmoMode.Transform, toolbar.Mode);

            foreach (var (button, mode) in new (FixedToggle, GizmoMode)[]
            {
                (toolbar.MoveButton, GizmoMode.Move),
                (toolbar.RotateButton, GizmoMode.Rotate),
                (toolbar.ScaleButton, GizmoMode.Scale),
                (toolbar.SelectButton, GizmoMode.Select),
                (toolbar.TransformButton, GizmoMode.Transform),
            })
            {
                ClickOn(button, window);
                Assert.Equal(mode, toolbar.Mode);
            }
        });
    }

    [Theory]
    [InlineData("Transform", false, GizmoSpace.Local)]
    [InlineData("Scale", false, GizmoSpace.Local)]
    [InlineData("Move", false, GizmoSpace.Global)]
    [InlineData("Rotate", false, GizmoSpace.Global)]
    [InlineData("Move", true, GizmoSpace.Local)]
    [InlineData("Rotate", true, GizmoSpace.Local)]
    public void Transform_and_scale_are_always_local_and_move_and_rotate_follow_the_local_button(string name, bool local, GizmoSpace space)
    {
        UiThread.Run(() =>
        {
            var mode = Enum.Parse<GizmoMode>(name);
            var window = OpenAtReferenceSize();
            var toolbar = window.Toolbar;
            ClickOn(mode switch
            {
                GizmoMode.Move => toolbar.MoveButton,
                GizmoMode.Rotate => toolbar.RotateButton,
                GizmoMode.Scale => toolbar.ScaleButton,
                _ => toolbar.TransformButton,
            }, window);
            if (local)
            {
                ClickOn(toolbar.LocalButton, window);
            }

            Assert.Equal(space, toolbar.Space);
        });
    }

    [Fact]
    public void The_trackball_and_snap_buttons_light_and_dim_and_say_nothing_is_missing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var toolbar = window.Toolbar;

            ClickOn(toolbar.TrackballButton, window);
            ClickOn(toolbar.SnapButton, window);
            Assert.True(toolbar.Trackball);
            Assert.True(toolbar.Snaps);
            ClickOn(toolbar.TrackballButton, window);
            ClickOn(toolbar.SnapButton, window);

            Assert.False(toolbar.Trackball);
            Assert.False(toolbar.Snaps);
            Assert.DoesNotContain("not built", window.Status);
        });
    }

    [Fact]
    public void The_snap_options_hold_three_steps_that_start_at_the_defaults()
    {
        UiThread.Run(() =>
        {
            var toolbar = OpenAtReferenceSize().Toolbar;

            Assert.IsType<Flyout>(toolbar.SnapOptionsButton.Flyout);
            Assert.Equal(GizmoSnap.Default, toolbar.Steps);
            Assert.Equal("1", toolbar.SnapMoveBox.Text);
            Assert.Equal("15", toolbar.SnapTurnBox.Text);
            Assert.Equal("0.1", toolbar.SnapScaleBox.Text);
        });
    }

    [Theory]
    [InlineData("2.5", 2.5f)]
    [InlineData(" 4 ", 4f)]
    [InlineData("0", 1f)]
    [InlineData("-3", 1f)]
    [InlineData("", 1f)]
    [InlineData("one", 1f)]
    [InlineData("NaN", 1f)]
    [InlineData("Infinity", 1f)]
    public void A_step_that_is_not_a_positive_number_falls_back_to_the_default(string text, float step)
    {
        UiThread.Run(() =>
        {
            var toolbar = OpenAtReferenceSize().Toolbar;

            toolbar.SnapMoveBox.Text = text;

            Assert.Equal(step, toolbar.Steps.Move);
        });
    }

    [Fact]
    public void Each_box_falls_back_to_its_own_default_step()
    {
        UiThread.Run(() =>
        {
            var toolbar = OpenAtReferenceSize().Toolbar;

            toolbar.SnapMoveBox.Text = "x";
            toolbar.SnapTurnBox.Text = "x";
            toolbar.SnapScaleBox.Text = "x";

            Assert.Equal(GizmoSnap.Default, toolbar.Steps);
        });
    }
}
