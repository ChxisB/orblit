using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;
using Vector2 = System.Numerics.Vector2;

namespace Orblit.Editor.App.Tests;

public sealed class PlanarViewportTests
{
    // A 100 by 50 frame in a 200 by 100 control is drawn at twice the size, so a frame pixel is two control pixels.
    // The mark sits at (50, 25) in the frame, which is (100, 50) in the control.
    static readonly Vector2 Mark = new(50, 25);

    static readonly Size FrameSize = new(100, 50);

    // Three rings around the mark. The Y ring has radius 20 and the others are smaller, so only it passes (70, 25).
    static readonly GizmoRings Rings = new(Mark, Circle(12), Circle(20), Circle(6));

    static Vector2[] Circle(float radius) =>
    [
        .. Enumerable.Range(0, GizmoMath.RingPoints).Select(i =>
            Mark + radius * new Vector2(MathF.Cos(MathF.Tau * i / GizmoMath.RingPoints), MathF.Sin(MathF.Tau * i / GizmoMath.RingPoints))),
    ];

    static bool Frame(out int width, out int height, ref byte[] rgba)
    {
        width = 100;
        height = 50;
        rgba = new byte[width * height * 4];
        return true;
    }

    static bool NoFrame(out int width, out int height, ref byte[] rgba)
    {
        width = 0;
        height = 0;
        return false;
    }

    sealed class Rig
    {
        public required Window Window { get; init; }

        public required ViewportControl Viewport { get; init; }

        public bool Planar { get; set; } = true;

        public GizmoMode Mode { get; set; } = GizmoMode.Move;

        public int MarksAsked { get; set; }

        public List<Vector2> Pans { get; } = [];

        public List<Vector2> Moves { get; } = [];

        public List<Vector2> Clicks { get; } = [];

        public List<(float Yaw, float Pitch)> Orbits { get; } = [];
    }

    static Rig Shown(bool hasMark = true, bool hasFrame = true)
    {
        Rig? rig = null;
        var viewport = new ViewportControl(() => false, hasFrame ? Frame : NoFrame)
        {
            PlanarSource = () => rig!.Planar,
            MarkSource = () => { rig!.MarksAsked++; return hasMark ? Mark : null; },
            HandlesSource = () => new GizmoHandles(new Vector2(20, 25), new Vector2(50, 25), new Vector2(20, 0), new Vector2(20, 25)),
            RingsSource = () => Rings,
            ModeSource = () => rig!.Mode,
        };
        rig = new Rig { Window = new Window { Width = 200, Height = 100, Content = viewport }, Viewport = viewport };
        viewport.PlanarPanned += rig.Pans.Add;
        viewport.PlanarMoved += move => rig.Moves.Add(move.Delta);
        viewport.Clicked += rig.Clicks.Add;
        viewport.Orbited += (yaw, pitch) => rig.Orbits.Add((yaw, pitch));
        rig.Window.Show();
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return rig;
    }

    [Fact]
    public void A_drag_that_starts_on_the_mark_raises_the_move_in_frame_pixels_and_does_not_pan()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();

            rig.Window.MouseDown(new Point(100, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(110, 50));
            rig.Window.MouseMove(new Point(110, 60));
            rig.Window.MouseUp(new Point(110, 60), MouseButton.Left);

            // The control draws at twice the frame, so 10 control pixels are 5 frame pixels.
            Assert.Equal([new Vector2(5, 0), new Vector2(0, 5)], rig.Moves);
            Assert.Empty(rig.Pans);
            Assert.Empty(rig.Orbits);
            Assert.Empty(rig.Clicks);
        });
    }

    [Fact]
    public void The_mark_moves_from_the_first_pixel()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();

            rig.Window.MouseDown(new Point(100, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(102, 50));

            Assert.Equal([new Vector2(1, 0)], rig.Moves);
        });
    }

    [Theory]
    [InlineData(112, 50, true)]
    [InlineData(100, 62, true)]
    [InlineData(88, 50, true)]
    [InlineData(113, 50, false)]
    [InlineData(100, 37.5, false)]
    public void A_press_within_12_pixels_of_the_mark_takes_it(double x, double y, bool takes)
    {
        UiThread.Run(() =>
        {
            var rig = Shown();

            rig.Window.MouseDown(new Point(x, y), MouseButton.Left);
            rig.Window.MouseMove(new Point(x + 20, y));

            Assert.Equal(takes ? 1 : 0, rig.Moves.Count);
            Assert.Equal(takes ? 0 : 1, rig.Pans.Count);
        });
    }

    [Fact]
    public void A_drag_in_empty_space_pans_by_the_distance_moved_in_frame_pixels_and_does_not_orbit()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();

            rig.Window.MouseDown(new Point(150, 80), MouseButton.Left);
            rig.Window.MouseMove(new Point(170, 80));
            rig.Window.MouseMove(new Point(170, 70));

            // The first move is the whole distance from the press, because the slop holds nothing back.
            Assert.Equal([new Vector2(10, 0), new Vector2(0, -5)], rig.Pans);
            Assert.Empty(rig.Moves);
            Assert.Empty(rig.Orbits);
            Assert.Empty(rig.Clicks);
        });
    }

    [Fact]
    public void A_wobble_under_the_slop_in_2D_is_a_click_that_pans_nothing()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();

            rig.Window.MouseDown(new Point(150, 80), MouseButton.Left);
            rig.Window.MouseMove(new Point(151, 81));
            rig.Window.MouseUp(new Point(151, 81), MouseButton.Left);

            Assert.Equal([new Vector2(75.5f, 40.5f)], rig.Clicks);
            Assert.Empty(rig.Pans);
        });
    }

    [Fact]
    public void A_press_on_the_mark_that_does_not_move_is_not_a_click()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();

            rig.Window.MouseDown(new Point(100, 50), MouseButton.Left);
            rig.Window.MouseUp(new Point(100, 50), MouseButton.Left);

            Assert.Empty(rig.Clicks);
            Assert.Empty(rig.Moves);
        });
    }

    [Fact]
    public void Letting_go_of_the_mark_ends_the_grab()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();
            rig.Window.MouseDown(new Point(100, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(105, 50));
            rig.Window.MouseUp(new Point(105, 50), MouseButton.Left);

            rig.Window.MouseDown(new Point(150, 80), MouseButton.Left);
            rig.Window.MouseMove(new Point(170, 80));

            Assert.Single(rig.Moves);
            Assert.Single(rig.Pans);
        });
    }

    [Fact]
    public void A_press_with_no_mark_to_take_pans()
    {
        UiThread.Run(() =>
        {
            var rig = Shown(hasMark: false);

            rig.Window.MouseDown(new Point(100, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(120, 50));

            Assert.Empty(rig.Moves);
            Assert.Single(rig.Pans);
        });
    }

    [Fact]
    public void A_press_where_the_mark_would_be_before_the_first_frame_takes_nothing()
    {
        UiThread.Run(() =>
        {
            var rig = Shown(hasFrame: false);

            rig.Window.MouseDown(new Point(100, 50), MouseButton.Left);
            rig.Window.MouseMove(new Point(120, 50));

            Assert.Empty(rig.Moves);
            Assert.Empty(rig.Pans);
        });
    }

    [Fact]
    public void Another_button_does_not_take_the_mark()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();

            rig.Window.MouseDown(new Point(100, 50), MouseButton.Right);
            rig.Window.MouseMove(new Point(110, 50));

            Assert.Empty(rig.Moves);
            Assert.Empty(rig.Pans);
        });
    }

    [Fact]
    public void Without_2D_a_drag_orbits_and_the_mark_is_not_asked_for()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();
            rig.Planar = false;
            rig.MarksAsked = 0;
            rig.Viewport.Tick();

            // Ten pixels from the mark and clear of the gizmo handle that ends at the mark.
            rig.Window.MouseDown(new Point(100, 60), MouseButton.Left);
            rig.Window.MouseMove(new Point(120, 60));

            Assert.Equal(0, rig.MarksAsked);
            Assert.Single(rig.Orbits);
            Assert.Empty(rig.Moves);
            Assert.Empty(rig.Pans);
        });
    }

    [Fact]
    public void The_mark_is_asked_for_on_each_tick_in_2D()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();
            var asked = rig.MarksAsked;

            rig.Viewport.Tick();

            Assert.Equal(asked + 1, rig.MarksAsked);
        });
    }

    [Fact]
    public void The_gizmo_is_gone_in_2D_and_back_in_3D()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();
            Assert.Null(rig.Viewport.HandleAt(new Point(80, 50)));

            rig.Planar = false;
            rig.Viewport.Tick();

            Assert.Equal(GizmoAxis.X, rig.Viewport.HandleAt(new Point(80, 50)));
        });
    }

    [Fact]
    public void The_rotate_rings_are_gone_in_2D_and_back_in_3D()
    {
        UiThread.Run(() =>
        {
            var rig = Shown();
            rig.Mode = GizmoMode.Rotate;
            rig.Viewport.Tick();
            // The ring of radius 20 frame pixels passes through (140, 50) in the control.
            var onRing = new Point(140, 50);
            Assert.Null(rig.Viewport.HitAt(onRing));

            rig.Planar = false;
            rig.Viewport.Tick();

            Assert.Equal(GizmoAxis.Y, rig.Viewport.HitAt(onRing)?.Axis);
        });
    }

    static readonly Vector2 Pivot = new(40, 20);

    static WorkspaceTab TabOf(EditorWindow window, string label) => window.Bar.Tabs.Single(tab => tab.Label == label);

    // The Lamp has a pivot at (40, 20) of the 100 by 50 frame. Opens the window with it selected, in 2D.
    static (EditorWindow Window, FakeScene Scene, ViewportControl Viewport) OpenIn2D(Action<FakeScene>? prepare = null)
    {
        var scene = new FakeScene();
        scene.Pivots[FakeScene.Lamp.Handle] = Pivot;
        prepare?.Invoke(scene);
        var window = OpenAtReferenceSize(scene);
        var viewport = new ViewportControl(() => false, Frame);
        window.ShowViewport(viewport);
        window.UpdateLayout();
        window.Outline.SelectedItem = FakeScene.Lamp;
        ClickOn(TabOf(window, TitleBar.Planar), window);
        window.UpdateLayout();
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, scene, viewport);
    }

    static Point InWindow(Control viewport, Visual window, Vector2 framePoint)
    {
        var control = ViewportControl.ControlAt(framePoint, FrameSize, viewport.Bounds.Size);
        return viewport.TranslatePoint(new Point(control.X, control.Y), window)!.Value;
    }

    static void AssertNear(Vector2 expected, Vector2? actual) =>
        Assert.True(actual is { } at && Vector2.Distance(expected, at) < 0.01f, $"{actual} is not {expected}");

    [Fact]
    public void The_2D_tab_turns_the_planar_view_on_and_every_other_tab_turns_it_off()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);

            ClickOn(TabOf(window, "2D"), window);
            Assert.Equal([true], scene.Planar);

            ClickOn(TabOf(window, "Asset Store"), window);
            ClickOn(TabOf(window, "2D"), window);
            ClickOn(TabOf(window, "3D"), window);

            Assert.Equal([true, false, true, false], scene.Planar);
        });
    }

    [Fact]
    public void The_2D_workspace_has_no_toolbar_or_projection_controls_and_the_viewport_takes_the_toolbar_row()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            ClickOn(TabOf(window, "2D"), window);
            window.UpdateLayout();

            Assert.False(window.Center.Toolbar.IsVisible);
            Assert.True(window.Center.Overlay.Planar);
            Assert.False(window.Center.Overlay.Axes.IsVisible);
            AssertBox(287, 74, 2866, 1157, BoxOf(window.Viewport, window));

            ClickOn(TabOf(window, "3D"), window);
            window.UpdateLayout();

            Assert.True(window.Center.Toolbar.IsVisible);
            Assert.False(window.Center.Overlay.Planar);
            Assert.True(window.Center.Overlay.Axes.IsVisible);
            AssertBox(287, 116, 2866, 1115, BoxOf(window.Viewport, window));
        });
    }

    [Fact]
    public void A_click_in_2D_picks_by_pivot_and_selects_the_result()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            window.ScenePane.Select(0);
            scene.PlanarPickResult = FakeScene.Sound.Handle;

            var at = InWindow(viewport, window, new Vector2(10, 10));
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);

            AssertNear(new Vector2(10, 10), scene.PlanarPickedAt);
            Assert.Null(scene.PickedAt);
            Assert.Equal("Sound", window.ScenePane.Selected!.Name);
        });
    }

    [Fact]
    public void A_click_in_3D_still_picks_by_ray()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            ClickOn(TabOf(window, "3D"), window);
            viewport.Tick();

            var at = InWindow(viewport, window, new Vector2(10, 10));
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);

            AssertNear(new Vector2(10, 10), scene.PickedAt);
            Assert.Null(scene.PlanarPickedAt);
        });
    }

    [Fact]
    public void The_selected_entity_2D_gives_the_mark_and_only_in_2D()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            Assert.Contains(FakeScene.Lamp.Handle, scene.PivotsAsked);

            scene.PivotsAsked.Clear();
            window.Outline.SelectedItem = FakeScene.Sound;
            viewport.Tick();
            Assert.Equal([FakeScene.Sound.Handle], scene.PivotsAsked);

            window.ScenePane.Select(0);
            scene.PivotsAsked.Clear();
            viewport.Tick();
            Assert.Empty(scene.PivotsAsked);

            window.Outline.SelectedItem = FakeScene.Lamp;
            ClickOn(TabOf(window, "3D"), window);
            scene.PivotsAsked.Clear();
            viewport.Tick();
            Assert.Empty(scene.PivotsAsked);
        });
    }

    [Fact]
    public void Dragging_the_mark_moves_the_selected_entity_and_says_so()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            scene.Highlighted.Clear();
            var onMark = InWindow(viewport, window, Pivot);

            window.MouseDown(onMark, MouseButton.Left);
            window.MouseMove(onMark + new Point(10, 0));

            var move = Assert.Single(scene.PlanarMoved);
            Assert.Equal(FakeScene.Lamp.Handle, move.Entity);
            Assert.True(move.Pixels.X > 0);
            Assert.Equal(0f, move.Pixels.Y);
            Assert.StartsWith("(*) ", window.Title);
            Assert.Equal("Moved Lamp. Save keeps the change.", window.Status);
            Assert.Equal("Lamp", window.ScenePane.Selected!.Name);
            Assert.Equal([FakeScene.Lamp.Handle], scene.Highlighted);
        });
    }

    [Fact]
    public void A_drag_on_the_mark_after_the_selection_went_does_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            var onMark = InWindow(viewport, window, Pivot);
            window.MouseDown(onMark, MouseButton.Left);
            window.ScenePane.Select(0);
            var status = window.Status;

            window.MouseMove(onMark + new Point(10, 0));

            Assert.Empty(scene.PlanarMoved);
            Assert.Equal(status, window.Status);
        });
    }

    [Fact]
    public void A_locked_entity_has_no_mark_to_take()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D(scene => scene.SetLocked(FakeScene.Lamp.Handle, true));
            scene.PivotsAsked.Clear();
            viewport.Tick();
            Assert.Empty(scene.PivotsAsked);
            var onPivot = InWindow(viewport, window, Pivot);

            window.MouseDown(onPivot, MouseButton.Left);
            window.MouseMove(onPivot + new Point(10, 0));

            Assert.Empty(scene.PlanarMoved);
        });
    }
}
