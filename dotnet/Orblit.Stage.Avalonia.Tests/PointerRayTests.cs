using Avalonia;
using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Avalonia.Tests;

public class PointerRayTests
{
    private static readonly Size View = new(100, 100);

    private static readonly RenderCamera Camera = new()
    {
        Position = new Vector3(0, 0, 5),
        Target = Vector3.Zero(),
        FieldOfView = 90,
    };

    // Where the ray meets the plane z = 0.
    private static Vector3 OnFloor(PointerRay ray) =>
        ray.Origin + (ray.Direction * (-ray.Origin.Z / ray.Direction.Z));

    [Fact]
    public void TheMiddleOfThePerspectiveViewLooksStraightAhead()
    {
        var ray = PointerRay.FromCamera(Camera, new Point(50, 50), View);

        Assert.Equal(new Vector3(0, 0, 5), ray.Origin);
        Assert.Equal(0, ray.Direction.X, 1e-9);
        Assert.Equal(0, ray.Direction.Y, 1e-9);
        Assert.Equal(-1, ray.Direction.Z, 1e-9);
    }

    [Fact]
    public void TheTopRightCornerIsARayThroughWhereTheMathsSays()
    {
        var ray = PointerRay.FromCamera(Camera, new Point(100, 0), View);

        // A right angle of view at five metres sees five metres each way.
        var hit = OnFloor(ray);
        Assert.Equal(5, hit.X, 1e-9);
        Assert.Equal(5, hit.Y, 1e-9);
    }

    [Fact]
    public void AWideViewStretchesSidewaysAndNotUpwards()
    {
        var ray = PointerRay.FromCamera(Camera, new Point(200, 0), new Size(200, 100));

        var hit = OnFloor(ray);
        Assert.Equal(10, hit.X, 1e-9);
        Assert.Equal(5, hit.Y, 1e-9);
    }

    [Fact]
    public void ATallViewMeasuresTheAngleAcrossItsWidth()
    {
        // The renderer measures the angle across the shorter side.
        var tall = new Size(100, 200);

        var side = OnFloor(PointerRay.FromCamera(Camera, new Point(100, 100), tall));
        var top = OnFloor(PointerRay.FromCamera(Camera, new Point(50, 0), tall));

        Assert.Equal(5, side.X, 1e-9);
        Assert.Equal(10, top.Y, 1e-9);
    }

    [Fact]
    public void EveryOrthographicRayRunsTheSameWayFromItsOwnPlace()
    {
        var flat = Camera with { Orthographic = true, ViewHeight = 10 };

        var ray = PointerRay.FromCamera(flat, new Point(100, 0), View);

        Assert.Equal(new Vector3(0, 0, -1), ray.Direction);
        Assert.Equal(5, ray.Origin.X, 1e-9);
        Assert.Equal(5, ray.Origin.Y, 1e-9);
        Assert.Equal(5, ray.Origin.Z, 1e-9);
    }

    [Fact]
    public void ACameraLookingStraightDownStillGivesARayAlongItsView()
    {
        var down = Camera with { Position = new Vector3(0, 10, 0) };

        var ray = PointerRay.FromCamera(down, new Point(30, 70), View);

        Assert.Equal(new Vector3(0, -1, 0), ray.Direction);
    }
}
