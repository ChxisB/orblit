using System.Numerics;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class GizmoMathTests
{
    // A pinhole camera with a 500 pixel focal length. Pixels are measured from the middle of the frame, y down.
    sealed class Pinhole(Vector3 eye, Vector3 target)
    {
        readonly Vector3 _forward = Vector3.Normalize(target - eye);

        Vector3 Right => Vector3.Normalize(Vector3.Cross(_forward, Vector3.UnitY));

        Vector3 Up => Vector3.Cross(Right, _forward);

        public Vector2 Project(Vector3 point)
        {
            var offset = point - eye;
            var depth = Vector3.Dot(offset, _forward);
            return new Vector2(500f * Vector3.Dot(offset, Right) / depth, -500f * Vector3.Dot(offset, Up) / depth);
        }

        public Ray Through(Vector2 pixel) =>
            new(eye, Vector3.Normalize(_forward * 500f + Right * pixel.X - Up * pixel.Y));

        public Ray At(Vector3 point) => Through(Project(point));
    }

    static readonly Pinhole Slanted = new(new Vector3(3, 4, 10), Vector3.Zero);

    static readonly Vector3 Diagonal = Vector3.Normalize(new Vector3(1, 1, 0));

    [Fact]
    public void A_ray_through_a_point_on_the_axis_is_nearest_at_that_point()
    {
        var along = GizmoMath.Along(Slanted.At(new Vector3(2, 2, 0)), Vector3.Zero, Diagonal);

        Assert.Equal(2f * MathF.Sqrt(2f), along!.Value, 3);
    }

    [Fact]
    public void A_ray_along_the_axis_has_no_nearest_point()
    {
        var ray = new Ray(new Vector3(0, 0, 10), new Vector3(0, 0, -1));

        Assert.Null(GizmoMath.Along(ray, Vector3.Zero, Vector3.UnitZ));
        Assert.Equal(0f, GizmoMath.Distance(ray, ray, Vector3.Zero, Vector3.UnitZ));
        Assert.Equal(1f, GizmoMath.ScaleFactor(ray, ray, Vector3.Zero, Vector3.UnitZ, 1f));
    }

    [Fact]
    public void A_move_slides_by_the_distance_the_pointer_took_it_along_the_axis()
    {
        var moved = GizmoMath.Distance(Slanted.At(Diagonal), Slanted.At(Diagonal * 4f), Vector3.Zero, Diagonal);

        Assert.Equal(3f, moved, 3);
    }

    [Fact]
    public void One_big_move_equals_ten_small_ones()
    {
        var steps = Enumerable.Range(0, 10).Sum(i =>
            GizmoMath.Distance(Slanted.At(Diagonal * (i * 0.4f)), Slanted.At(Diagonal * ((i + 1) * 0.4f)), Vector3.Zero, Diagonal));
        var jump = GizmoMath.Distance(Slanted.At(Vector3.Zero), Slanted.At(Diagonal * 4f), Vector3.Zero, Diagonal);

        Assert.Equal(4f, jump, 3);
        Assert.Equal(jump, steps, 3);
    }

    [Fact]
    public void A_scale_drag_multiplies_by_how_much_farther_from_the_pivot_the_pointer_is()
    {
        var factor = GizmoMath.ScaleFactor(Slanted.At(Diagonal), Slanted.At(Diagonal * 2.5f), Vector3.Zero, Diagonal, 1f);

        Assert.Equal(2.5f, factor, 3);
    }

    [Fact]
    public void Ten_small_scale_drags_multiply_to_the_factor_of_one_big_one()
    {
        var product = 1f;
        for (var i = 0; i < 10; i++)
        {
            product *= GizmoMath.ScaleFactor(Slanted.At(Diagonal * (1f + i * 0.2f)), Slanted.At(Diagonal * (1f + (i + 1) * 0.2f)), Vector3.Zero, Diagonal, 1f);
        }

        Assert.Equal(3f, product, 3);
    }

    [Fact]
    public void A_scale_drag_that_starts_at_the_pivot_changes_nothing()
    {
        Assert.Equal(1f, GizmoMath.ScaleFactor(Slanted.At(Vector3.Zero), Slanted.At(Diagonal * 3f), Vector3.Zero, Diagonal, 1f));
    }

    [Fact]
    public void A_scale_value_is_the_old_one_times_the_factor_on_its_axis_alone()
    {
        Assert.Equal(new Vector3(2, 4.5f, 4), GizmoMath.Scaled(new Vector3(2, 3, 4), GizmoAxis.Y, 1.5f));
    }

    [Theory]
    [InlineData(0f, 2f, 0.1f)]
    [InlineData(0.05f, 2f, 0.15f)]
    [InlineData(-0.05f, 2f, -0.15f)]
    [InlineData(0f, 0.5f, 0f)]
    [InlineData(0f, 1f, 0f)]
    public void An_axis_scaled_to_nothing_leaves_zero_by_a_step_when_it_grows(float value, float factor, float expected)
    {
        var scaled = GizmoMath.Scaled(new Vector3(value, 2, 3), GizmoAxis.X, factor);

        Assert.Equal(new Vector3(expected, 2, 3), scaled);
    }

    [Fact]
    public void A_uniform_scale_drag_doubles_when_the_pointer_goes_from_the_pivot_to_a_handles_length_out()
    {
        var origin = new Vector2(100, 100);

        Assert.Equal(2f, GizmoMath.UniformFactor(origin, new FrameMove(origin, origin + new Vector2(GizmoMath.HandleLength, 0))), 4);
        Assert.Equal(0.5f, GizmoMath.UniformFactor(origin, new FrameMove(origin + new Vector2(0, GizmoMath.HandleLength), origin)), 4);
        Assert.Equal(1f, GizmoMath.UniformFactor(origin, new FrameMove(origin + new Vector2(30, 40), origin + new Vector2(-40, 30))), 4);
    }

    [Fact]
    public void Ten_small_uniform_scale_drags_multiply_to_the_factor_of_one_big_one()
    {
        var origin = new Vector2(100, 100);
        var product = 1f;
        for (var i = 0; i < 10; i++)
        {
            product *= GizmoMath.UniformFactor(origin, new FrameMove(origin + new Vector2(i * 20, 0), origin + new Vector2((i + 1) * 20, 0)));
        }

        Assert.Equal(GizmoMath.UniformFactor(origin, new FrameMove(origin, origin + new Vector2(200, 0))), product, 3);
    }

    [Fact]
    public void A_scale_of_all_three_values_takes_the_zero_escape_on_each()
    {
        Assert.Equal(new Vector3(4, 6, 8), GizmoMath.ScaledAll(new Vector3(2, 3, 4), 2f));
        Assert.Equal(new Vector3(0.1f, 4, -0.15f), GizmoMath.ScaledAll(new Vector3(0, 2, -0.05f), 2f));
    }

    [Theory]
    [InlineData(0.4f, 0.5f, 0.5f)]
    [InlineData(0.25f, 0.5f, 0.5f)]
    [InlineData(-0.25f, 0.5f, -0.5f)]
    [InlineData(0.24f, 0.5f, 0f)]
    [InlineData(1.7f, 1f, 2f)]
    [InlineData(1.7f, 0f, 1.7f)]
    [InlineData(1.7f, -1f, 1.7f)]
    [InlineData(1.7f, float.NaN, 1.7f)]
    public void A_drag_total_is_held_to_the_nearest_step_and_no_step_leaves_it_alone(float total, float step, float expected)
    {
        Assert.Equal(expected, GizmoMath.Stepped(total, step), 5);
    }

    [Fact]
    public void A_spin_is_the_angle_the_pointer_swept_round_the_pivot_clockwise_on_screen()
    {
        var origin = new Vector2(50, 50);

        Assert.Equal(MathF.PI / 2f, GizmoMath.Spun(origin, new FrameMove(origin + new Vector2(10, 0), origin + new Vector2(0, 10))), 4);
        Assert.Equal(-MathF.PI / 2f, GizmoMath.Spun(origin, new FrameMove(origin + new Vector2(10, 0), origin + new Vector2(0, -10))), 4);
        Assert.Equal(MathF.PI * 3f / 4f, GizmoMath.Spun(origin, new FrameMove(origin + new Vector2(10, 0), origin + new Vector2(-10, 10))), 4);
    }

    [Fact]
    public void A_spin_that_starts_or_ends_on_the_pivot_has_no_angle()
    {
        var origin = new Vector2(50, 50);

        Assert.Equal(0f, GizmoMath.Spun(origin, new FrameMove(origin, origin + new Vector2(30, 0))));
        Assert.Equal(0f, GizmoMath.Spun(origin, new FrameMove(origin + new Vector2(30, 0), origin + new Vector2(0.5f, 0))));
        Assert.Equal(0f, GizmoMath.Spun(origin, new FrameMove(origin + new Vector2(0.5f, 0), origin + new Vector2(0, 30))));
    }

    [Fact]
    public void Many_small_spins_add_to_one_big_spin()
    {
        var origin = new Vector2(50, 50);
        Vector2 At(float degrees) => origin + 40f * new Vector2(MathF.Cos(degrees * MathF.PI / 180f), MathF.Sin(degrees * MathF.PI / 180f));
        var steps = Enumerable.Range(0, 12).Sum(i => GizmoMath.Spun(origin, new FrameMove(At(i * 10f), At((i + 1) * 10f))));

        Assert.Equal(2f * MathF.PI / 3f, steps, 3);
    }

    [Fact]
    public void A_ball_dragged_right_turns_its_front_to_the_right_and_dragged_down_turns_it_down()
    {
        var forward = -Vector3.UnitZ;
        var front = -forward;

        var right = Vector3.Transform(front, GizmoMath.Rolled(forward, new FrameMove(Vector2.Zero, new Vector2(GizmoMath.HandleLength, 0))));
        var down = Vector3.Transform(front, GizmoMath.Rolled(forward, new FrameMove(Vector2.Zero, new Vector2(0, GizmoMath.HandleLength / 2f))));

        AssertNear(new Vector3(MathF.Sin(1f), 0, MathF.Cos(1f)), right);
        AssertNear(new Vector3(0, -MathF.Sin(0.5f), MathF.Cos(0.5f)), down);
    }

    static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-5f, $"{expected} is not near {actual}");

    [Fact]
    public void A_ball_dragged_across_a_tilted_view_follows_the_pointer_on_screen()
    {
        var forward = Vector3.Normalize(-new Vector3(3, 4, 10));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

        var turned = Vector3.Transform(-forward, GizmoMath.Rolled(forward, new FrameMove(Vector2.Zero, new Vector2(20, 0))));

        Assert.True(Vector3.Dot(turned + forward, right) > 0.2f);
        Assert.Equal(1f, turned.Length(), 4);
    }

    [Fact]
    public void A_ball_dragged_diagonally_on_a_steep_view_follows_the_pointer_on_screen()
    {
        var forward = Vector3.Normalize(-new Vector3(3, 10, 4));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Cross(right, forward);

        var turned = Vector3.Transform(-forward, GizmoMath.Rolled(forward, new FrameMove(Vector2.Zero, new Vector2(5, 5))));

        // Right and down on screen, in equal parts, so the front moves right as far as it moves down.
        var across = Vector3.Dot(turned + forward, right);
        var down = -Vector3.Dot(turned + forward, up);
        Assert.True(across > 0.05f);
        Assert.True(MathF.Abs(across - down) < 0.1f * across, $"the front went {across} across and {down} down");
    }

    [Fact]
    public void A_ball_that_does_not_move_does_not_turn()
    {
        Assert.Equal(Quaternion.Identity, GizmoMath.Rolled(-Vector3.UnitZ, new FrameMove(new Vector2(5, 5), new Vector2(5, 5))));
    }

    [Fact]
    public void A_turn_is_the_angle_the_pointer_swept_in_the_ring_plane()
    {
        var swept = GizmoMath.Swept(Slanted.At(Vector3.UnitX), Slanted.At(-Vector3.UnitZ), Vector3.Zero, Vector3.UnitY);

        Assert.Equal(MathF.PI / 2f, swept!.Value, 3);
    }

    [Fact]
    public void Ten_small_turns_add_to_one_big_turn()
    {
        Vector3 OnRing(float degrees) => new(MathF.Cos(degrees * MathF.PI / 180f), 0, -MathF.Sin(degrees * MathF.PI / 180f));
        var steps = Enumerable.Range(0, 10).Sum(i =>
            GizmoMath.Swept(Slanted.At(OnRing(i * 9f)), Slanted.At(OnRing((i + 1) * 9f)), Vector3.Zero, Vector3.UnitY)!.Value);
        var jump = GizmoMath.Swept(Slanted.At(OnRing(0f)), Slanted.At(OnRing(90f)), Vector3.Zero, Vector3.UnitY)!.Value;

        Assert.Equal(MathF.PI / 2f, jump, 3);
        Assert.Equal(jump, steps, 3);
    }

    [Fact]
    public void A_ring_seen_edge_on_has_no_swept_angle()
    {
        var level = new Pinhole(new Vector3(0, 0, 10), Vector3.Zero);

        Assert.Null(GizmoMath.Swept(level.At(Vector3.UnitX), level.At(-Vector3.UnitX), Vector3.Zero, Vector3.UnitY));
    }

    [Theory]
    [InlineData(10f, 0f, 0f)]
    [InlineData(0f, 5f, 10f)]
    [InlineData(4f, 4f, 4f)]
    [InlineData(1f, 1f, 1.5f)]
    public void The_rings_reach_80_pixels_on_average_from_wherever_the_camera_is(float x, float y, float z)
    {
        var eye = new Vector3(x, y, z + 0.01f);
        var camera = new Pinhole(eye, Vector3.Zero);
        var axes = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };

        var radius = GizmoMath.RingRadius(camera.Project, Vector3.Zero, axes, GizmoMath.ProbeShare * eye.Length());

        var origin = camera.Project(Vector3.Zero);
        float Reach(int stride) => axes.SelectMany(axis => GizmoMath.Ring(Vector3.Zero, axis, radius).Where((_, i) => i % stride == 0))
            .Average(point => Vector2.Distance(camera.Project(point), origin));

        // The radius is solved on every sixth point, so those settle exactly. A denser sample sits a little off.
        Assert.Equal(GizmoMath.HandleLength, Reach(6), 1);
        Assert.InRange(Reach(3), GizmoMath.HandleLength - 3f, GizmoMath.HandleLength + 3f);
    }
}
