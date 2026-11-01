using Orblit.VectorMath;

namespace Orblit.Light.Tests;

public class LightSourceTests
{
    [Fact]
    public void ASunIsTheWidthTheRealOneIs()
    {
        var angle = new LightSource(LightType.Sun).SunAngle;
        Assert.True(
            angle == 0.526,
            $"{angle}: the angular diameter of the sun, and why outdoor shadows soften with distance");
    }

    [Fact]
    public void ASpotOpensToFortyFiveDegreesWithASoftRim()
    {
        var spot = new LightSource(LightType.Spot);
        Assert.Equal(45, spot.SpotSize);
        Assert.Equal(0.15, spot.SpotBlend);
    }

    [Fact]
    public void PowerStartsWhereEachTypeStartsInTheTool()
    {
        Assert.Equal(1000, new LightSource(LightType.Point).Power);
        Assert.Equal(1000, new LightSource(LightType.Spot).Power);
        Assert.Equal(100, new LightSource(LightType.Area).Power);
        Assert.True(
            new LightSource(LightType.Sun).Power == 1,
            "a sun is stated per square metre, so its number is small");
    }

    [Fact]
    public void APointSourceHasSizeSoItsShadowsHaveAnEdge() => Assert.Equal(0.1, new LightSource().Radius);

    [Fact]
    public void APointLightArrivesInLumens()
    {
        var rendered = new LightSource(LightType.Point, power: 100).ToRenderer();
        Assert.Equal(RendererLightKind.Point, rendered.Kind);
        Assert.Equal(68300, rendered.Intensity, 1e-6);
    }

    [Fact]
    public void ASunArrivesInLuxAndNeverFallsOff()
    {
        var rendered = new LightSource(LightType.Sun, power: 3).ToRenderer();
        Assert.Equal(RendererLightKind.Directional, rendered.Kind);
        Assert.Equal(2049, rendered.Intensity, 1e-6);
        Assert.True(
            double.IsPositiveInfinity(rendered.FalloffRadius),
            "parallel rays do not weaken with distance, and giving them a radius would clip the world");
    }

    [Fact]
    public void ASunReportsHalfItsAngularDiameter()
    {
        // Renderers ask for the radius of the disc, not its width.
        var rendered = new LightSource(LightType.Sun, sunAngle: 1.0).ToRenderer();
        Assert.Equal(0.5, rendered.SunAngularRadius, 1e-9);
    }

    [Fact]
    public void ASpotConeBecomesInnerAndOuterAnglesInRadians()
    {
        var rendered = new LightSource(LightType.Spot, spotSize: 60, spotBlend: 0.25).ToRenderer();

        // Half of sixty degrees, in radians.
        Assert.Equal(30 * Math.PI / 180, rendered.OuterConeAngle, 1e-9);

        // Blend eats a quarter of the cone from the outside in.
        Assert.Equal(30 * 0.75 * Math.PI / 180, rendered.InnerConeAngle, 1e-9);
    }

    [Fact]
    public void AHardEdgedSpotHasNoFalloffBand()
    {
        var rendered = new LightSource(LightType.Spot, spotBlend: 0).ToRenderer();
        Assert.Equal(rendered.OuterConeAngle, rendered.InnerConeAngle, 1e-12);
    }

    [Fact]
    public void AFullyBlendedSpotIsGradientAllTheWayToTheCentre()
    {
        var rendered = new LightSource(LightType.Spot, spotBlend: 1).ToRenderer();
        Assert.Equal(0, rendered.InnerConeAngle, 1e-12);
    }

    [Fact]
    public void ACustomDistanceOverridesTheDerivedOne()
    {
        var derived = new LightSource(LightType.Point, power: 1000).ToRenderer();
        var fixedDistance = new LightSource(LightType.Point, power: 1000, customDistance: 12).ToRenderer();

        Assert.Equal(12, fixedDistance.FalloffRadius);
        Assert.NotEqual(12, derived.FalloffRadius);
    }

    [Fact]
    public void AStraightEdgedAreaLightIsNotApproximatedAtAll()
    {
        // The renderer shades rectangles, and a square is a rectangle, so
        // nothing is lost on the way.
        var square = new LightSource(LightType.Area).ToRenderer();
        Assert.Equal(RendererLightKind.Area, square.Kind);
        Assert.False(square.Approximated);
        Assert.False(new LightSource(LightType.Point).ToRenderer().Approximated);
    }

    [Fact]
    public void ARoundAreaLightIsSquaredOffAndSaysSo()
    {
        // The closed form the shading uses is for polygons, so a disk has to
        // become a rectangle. Silently substituting one is how a scene stops
        // matching its reference without anyone knowing why.
        var disk = new LightSource(LightType.Area, shape: AreaShape.Disk, sizeX: 2).ToRenderer();
        Assert.Equal(RendererLightKind.Area, disk.Kind);
        Assert.True(disk.Approximated, "a disk is not a rectangle and the outline it reflects differs");

        // Matched on area, not on bounding box: the square around a disk is
        // over a quarter too big, and area is what sets the brightness.
        Assert.Equal(Math.PI * 1 * 1, disk.Width * disk.Height, 1e-9);
    }

    [Fact]
    public void ARectangleKeepsTheEdgesItWasGiven()
    {
        var rendered = new LightSource(LightType.Area, shape: AreaShape.Rectangle, sizeX: 3, sizeY: 0.5).ToRenderer();
        Assert.Equal(3, rendered.Width);
        Assert.Equal(0.5, rendered.Height);
    }

    [Fact]
    public void AnApproximatedAreaLightKeepsABelievablePenumbra()
    {
        // A one metre square becomes the sphere with the same area, so its
        // shadow edge is about as soft as the real shape's would have been.
        var rendered = new LightSource(LightType.Area, shape: AreaShape.Square, sizeX: 1).ToRenderer();
        Assert.Equal(Math.Sqrt(1 / Math.PI), rendered.SourceRadius, 1e-9);
    }

    [Fact]
    public void ASquareUsesOneDimension() =>
        Assert.Equal(4, new LightSource(LightType.Area, sizeX: 2, sizeY: 99).EmittingArea);

    [Fact]
    public void ARectangleUsesBoth() =>
        Assert.Equal(
            6,
            new LightSource(LightType.Area, shape: AreaShape.Rectangle, sizeX: 2, sizeY: 3).EmittingArea);

    [Fact]
    public void ADiskIsACircleOfThatDiameter() =>
        Assert.Equal(
            Math.PI,
            new LightSource(LightType.Area, shape: AreaShape.Disk, sizeX: 2).EmittingArea,
            1e-9);

    [Fact]
    public void AnEllipseUsesBothRadii() =>
        Assert.Equal(
            Math.PI * 2 * 1,
            new LightSource(LightType.Area, shape: AreaShape.Ellipse, sizeX: 4, sizeY: 2).EmittingArea,
            1e-9);

    [Fact]
    public void CopiesDoNotShareTheirColour()
    {
        var original = new LightSource(color: new Vector3(1, 0.5, 0.2));
        var copy = original.Copy();
        copy.Color.X = 0;

        Assert.Equal(0, copy.Color.X);
        Assert.True(
            original.Color.X == 1,
            "a duplicated light that shares a vector is a bug that only shows up once somebody edits one of them");
    }

    [Fact]
    public void ResponseMultipliersExistBecausePhysicsDoesNotHaveThem()
    {
        // Killing specular on a fill light is how you stop a second highlight
        // appearing in a character's eyes. No real fixture will do that.
        var light = new LightSource(specular: 0);
        Assert.Equal(0, light.Specular);
        Assert.Equal(1, light.Diffuse);
    }
}
