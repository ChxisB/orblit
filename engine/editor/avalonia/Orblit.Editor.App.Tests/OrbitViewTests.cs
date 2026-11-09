using System.Numerics;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class OrbitViewTests
{
    static readonly OrbitView Flat = new(Vector3.Zero, 0, 0, 10);

    static void AssertNear(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"{actual} is not {expected}");

    [Fact]
    public void Facing_the_front_the_eye_is_in_front_of_the_target()
    {
        AssertNear(new Vector3(0, 0, 10), Flat.Eye);
    }

    [Fact]
    public void A_quarter_turn_moves_the_eye_to_the_side()
    {
        AssertNear(new Vector3(10, 0, 0), Flat.Orbited(MathF.PI / 2, 0).Eye);
    }

    [Fact]
    public void Pitch_lifts_the_eye_above_the_target()
    {
        var eye = Flat.Orbited(0, MathF.PI / 6).Eye;

        Assert.True(eye.Y > 4.9f && eye.Y < 5.1f);
    }

    [Fact]
    public void Framing_moves_the_target_and_keeps_the_angles_and_the_distance()
    {
        var before = Flat.Orbited(0.5f, 0.2f).Zoomed(2);
        var target = new Vector3(3, 4, 5);

        var framed = before.Framed(target);

        Assert.Equal(target, framed.Target);
        Assert.Equal((before.Yaw, before.Pitch, before.Distance), (framed.Yaw, framed.Pitch, framed.Distance));
        AssertNear(target + (before.Eye - before.Target), framed.Eye);
    }

    [Fact]
    public void The_eye_stays_at_the_same_distance()
    {
        var view = Flat.Orbited(1.3f, 0.7f);

        Assert.Equal(10f, Vector3.Distance(view.Target, view.Eye), 3);
    }

    [Fact]
    public void Pitch_stops_short_of_straight_up_and_down()
    {
        Assert.True(Flat.Orbited(0, 100).Pitch < MathF.PI / 2);
        Assert.True(Flat.Orbited(0, -100).Pitch > -MathF.PI / 2);
    }

    [Fact]
    public void Zooming_in_brings_the_eye_closer_and_zooming_out_takes_it_back()
    {
        Assert.True(Flat.Zoomed(1).Distance < 10f);
        Assert.Equal(10f, Flat.Zoomed(1).Zoomed(-1).Distance, 3);
        Assert.True(Flat.Zoomed(-1).Distance > 10f);
    }

    [Fact]
    public void Zooming_in_stops_at_a_minimum_distance()
    {
        Assert.True(Flat.Zoomed(1000).Distance > 0.4f);
    }

    [Fact]
    public void A_move_leaves_the_old_view_alone()
    {
        _ = Flat.Orbited(1, 0.2f);

        Assert.Equal(0f, Flat.Yaw);
        Assert.Equal(0f, Flat.Pitch);
    }
}
