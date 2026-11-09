using System.Numerics;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class PlanarViewTests
{
    static readonly Vector2 Size = new(320, 240);

    static void AssertNear(Vector2 expected, Vector2 actual) => Assert.True(Vector2.Distance(expected, actual) < 1e-3f, $"{actual} is not {expected}");

    [Fact]
    public void The_centre_lands_in_the_middle_of_the_frame()
    {
        var view = new PlanarView(new Vector2(40, -10), 2);

        AssertNear(new Vector2(160, 120), view.Project(new Vector2(40, -10), Size));
    }

    [Fact]
    public void A_point_one_unit_right_of_the_centre_lands_zoom_pixels_to_the_right()
    {
        var view = new PlanarView(Vector2.Zero, 4);

        AssertNear(new Vector2(164, 120), view.Project(new Vector2(1, 0), Size));
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(1f)]
    [InlineData(8f)]
    public void Panning_moves_what_is_drawn_with_the_pointer_at_any_zoom(float zoom)
    {
        var view = new PlanarView(new Vector2(5, 5), zoom);
        var world = new Vector2(30, -20);
        var before = view.Project(world, Size);

        var after = view.Panned(new Vector2(17, -9)).Project(world, Size);

        AssertNear(before + new Vector2(17, -9), after);
    }

    [Fact]
    public void Zooming_in_makes_things_larger_and_keeps_the_centre()
    {
        var view = new PlanarView(new Vector2(7, 8), 1);

        var zoomed = view.Zoomed(3);

        Assert.True(zoomed.Zoom > 1.3f && zoomed.Zoom < 1.4f);
        Assert.Equal(view.Center, zoomed.Center);
    }

    [Fact]
    public void Zooming_out_undoes_zooming_in()
    {
        var zoomed = PlanarView.Start.Zoomed(5).Zoomed(-5);

        Assert.Equal(1f, zoomed.Zoom, 4);
    }

    [Fact]
    public void The_zoom_stops_at_both_ends()
    {
        Assert.Equal(32f, PlanarView.Start.Zoomed(1000).Zoom);
        Assert.Equal(0.05f, PlanarView.Start.Zoomed(-1000).Zoom);
    }
}
