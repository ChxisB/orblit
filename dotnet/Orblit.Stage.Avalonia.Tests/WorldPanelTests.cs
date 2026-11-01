using Avalonia;
using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Avalonia.Tests;

public class WorldPanelTests
{
    // A 200 by 100 panel on a wall: turned to face along +x, two metres out sideways and twice as
    // wide as it is tall.
    private static readonly Matrix4 Wall = Matrix4.Compose(
        new Vector3(2, 1, 0),
        Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 2),
        new Vector3(2, 1, 0.05));

    private static readonly Vector3 Eye = new(10, 1, 0);

    // A ray from `from` to the point `local` on a quad placed by `transform`. A quad's own space runs
    // from -1 to 1, with its front face at z = 1.
    private static PointerRay RayTo(Matrix4 transform, Vector3 from, Vector3 local)
    {
        var target = transform.Transformed3(local);
        return new PointerRay(from, (target - from).Normalized());
    }

    private static double[] Cells(Matrix4 matrix) => [.. Enumerable.Range(0, 16).Select(at => matrix[at])];

    // Rays are worked through an inverse, so a landing is exact only to rounding.
    private static void AssertAt(double x, double y, Point actual)
    {
        Assert.Equal(x, actual.X, 1e-6);
        Assert.Equal(y, actual.Y, 1e-6);
    }

    private static (WorldPanel Wall, FakePanel Panel, RecordedResources Resources) Make()
    {
        var panel = new FakePanel();
        var resources = new RecordedResources();
        var wall = new WorldPanel(
            panel,
            Wall,
            new WorldPanelOptions
            {
                Id = "wall",
                ObjectKey = 7,
                MaterialKey = 8,
                Provide = resources.Provide,
                Release = resources.Release,
            });
        return (wall, panel, resources);
    }

    [Fact]
    public void TheMiddleOfTheFaceIsTheMiddleOfThePicture()
    {
        var (wall, _, _) = Make();

        var pixel = wall.PixelAt(RayTo(Wall, Eye, new Vector3(0, 0, 1)));

        Assert.Equal(100, pixel!.Value.X, 1e-6);
        Assert.Equal(50, pixel.Value.Y, 1e-6);
    }

    [Fact]
    public void UpOnTheFaceIsUpInThePicture()
    {
        var (wall, _, _) = Make();

        var pixel = wall.PixelAt(RayTo(Wall, Eye, new Vector3(0.5, 0.5, 1)));

        Assert.Equal(150, pixel!.Value.X, 1e-6);
        Assert.Equal(25, pixel.Value.Y, 1e-6);
    }

    [Fact]
    public void ARayThatLandsOffTheFaceMisses()
    {
        var (wall, _, _) = Make();

        Assert.False(wall.Hits(RayTo(Wall, Eye, new Vector3(1.5, 0, 1))));
    }

    [Fact]
    public void ARayFromBehindTheFaceMisses()
    {
        var (wall, _, _) = Make();

        Assert.False(wall.Hits(RayTo(Wall, new Vector3(-6, 1, 0), new Vector3(0, 0, 1))));
    }

    [Fact]
    public void ARayThatStartsBehindTheFaceAndRunsAwayMisses()
    {
        var (wall, _, _) = Make();

        // The face is at x = 2.05 looking out along +x. This ray is at x = -6 going further back, so the
        // plane is behind it.
        Assert.False(wall.Hits(new PointerRay(new Vector3(-6, 1, 0), new Vector3(-1, 0, 0))));
    }

    [Fact]
    public void ARayRunningAlongTheFaceMisses()
    {
        var (wall, _, _) = Make();

        Assert.False(wall.Hits(new PointerRay(new Vector3(10, 1, 0), new Vector3(0, 0, 1))));
    }

    [Fact]
    public void MovingThePanelMovesWhereItIsHit()
    {
        var (wall, _, _) = Make();
        var ray = RayTo(Wall, Eye, new Vector3(0, 0, 1));

        wall.Place(Matrix4.TranslationValues(0, 50, 0));

        Assert.False(wall.Hits(ray));
    }

    [Theory]
    [InlineData(-1, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(0, 1, true)]
    public void ThePicturesFarEdgesAreOffTheFaceAndItsNearEdgesAreOn(double x, double y, bool hit)
    {
        var panel = new FakePanel();
        var flat = new WorldPanel(panel, Matrix4.Identity(), new WorldPanelOptions { Id = "flat", ObjectKey = 1, MaterialKey = 2 });

        // Straight down the z axis onto the unit face, whose corners land on the picture's corners.
        Assert.Equal(hit, flat.Hits(new PointerRay(new Vector3(x, y, 5), new Vector3(0, 0, -1))));
    }

    [Fact]
    public void APressAndReleaseOnTheFaceReachThePanelAtThePixel()
    {
        var (wall, panel, _) = Make();
        var ray = RayTo(Wall, Eye, new Vector3(0, 0, 1));

        wall.Down(ray);
        wall.Up(ray);

        Assert.Equal(new[] { PointerPhase.Down, PointerPhase.Up }, panel.Sent.Select(p => p.Phase));
        Assert.All(panel.Sent, p => Assert.Equal(1, p.Id));
        Assert.All(panel.Sent, p => AssertAt(100, 50, p.Position));
    }

    [Fact]
    public void APressThatMissesTheFaceDoesNothing()
    {
        var (wall, panel, _) = Make();
        var off = RayTo(Wall, Eye, new Vector3(1.5, 0, 1));

        wall.Down(off);
        wall.Up(off);

        // A release goes to wherever the ray meets the plane, so only the press is checked.
        Assert.DoesNotContain(panel.Sent, p => p.Phase == PointerPhase.Down);
    }

    [Fact]
    public void ACancelledPressIsReportedAtTheLastPixel()
    {
        var (wall, panel, _) = Make();
        var ray = RayTo(Wall, Eye, new Vector3(0, 0, 1));

        wall.Down(ray);
        wall.Cancel();

        Assert.Equal(new[] { PointerPhase.Down, PointerPhase.Cancel }, panel.Sent.Select(p => p.Phase));
        Assert.Equal(panel.Sent[0].Position, panel.Sent[1].Position);
    }

    [Fact]
    public void CancellingWithNoPressSendsNothing()
    {
        var (wall, panel, _) = Make();

        wall.Cancel();

        Assert.Empty(panel.Sent);
    }

    [Fact]
    public void ADragKeepsReportingAfterItLeavesTheFace()
    {
        var (wall, panel, _) = Make();
        var start = RayTo(Wall, Eye, new Vector3(-0.5, 0, 1));
        var inside = RayTo(Wall, Eye, new Vector3(0, 0, 1));
        var outside = RayTo(Wall, Eye, new Vector3(1.8, 0, 1));

        wall.Down(start);
        wall.Move(inside);
        wall.Move(outside);
        wall.Up(outside);

        var across = panel.Sent.Select(p => Math.Round(p.Position.X, 6));
        Assert.Equal(new double[] { 50, 100, 280, 280 }, across);
        Assert.Equal(
            new[] { PointerPhase.Down, PointerPhase.Move, PointerPhase.Move, PointerPhase.Up },
            panel.Sent.Select(p => p.Phase));
    }

    [Fact]
    public void AMoveThatMissesThePlaneStaysWhereThePressLastWas()
    {
        var (wall, panel, _) = Make();
        wall.Down(RayTo(Wall, Eye, new Vector3(0, 0, 1)));

        wall.Move(new PointerRay(new Vector3(10, 1, 0), new Vector3(0, 0, 1)));

        AssertAt(100, 50, panel.Sent[1].Position);
    }

    [Fact]
    public void ARayLevelWithTheFaceToWithinRoundingMissesIt()
    {
        var (wall, panel, _) = Make();

        // Tilted towards the face by a trillionth, which meets its plane a million million metres away.
        var level = new PointerRay(Eye, new Vector3(-1e-12, 0, 1).Normalized());
        wall.Move(level);

        Assert.Null(wall.PixelAt(level));
        Assert.Empty(panel.Sent);
    }

    [Fact]
    public void AMoveWithNothingPressedAndNoPlaneSendsNothing()
    {
        var (wall, panel, _) = Make();

        wall.Move(new PointerRay(new Vector3(10, 1, 0), new Vector3(0, 0, 1)));

        Assert.Empty(panel.Sent);
    }

    [Fact]
    public void TheFirstPictureIsProvidedAndDrawn()
    {
        var (wall, _, resources) = Make();

        wall.Sync();

        var name = resources.Provided.Single();
        Assert.StartsWith("orblit:resource/panel/wall/", name);
        Assert.EndsWith(".png", name);
        Assert.Equal(name, wall.Material.BaseColourMap!.Path);
        Assert.Equal(8L, wall.Quad.Material);
        Assert.Equal(7L, wall.Quad.Key);
    }

    [Fact]
    public void AChangeIsANewNameAndTheOldOneIsReleased()
    {
        var (wall, panel, resources) = Make();
        wall.Sync();
        var first = wall.Material.BaseColourMap!.Path;

        panel.Change();
        wall.Sync();

        Assert.NotEqual(first, wall.Material.BaseColourMap!.Path);
        Assert.Equal(first, resources.Released.Single());
    }

    [Fact]
    public void AnUnchangedPanelSendsNothing()
    {
        var (wall, _, resources) = Make();
        wall.Sync();
        resources.Calls.Clear();

        wall.Sync();

        Assert.Empty(resources.Calls);
    }

    [Fact]
    public void LettingGoReleasesTheLastPicture()
    {
        var (wall, _, resources) = Make();
        wall.Sync();
        var name = wall.Material.BaseColourMap!.Path;

        wall.Dispose();
        wall.Dispose();

        Assert.Equal(name, resources.Released.Single());
    }

    [Fact]
    public void BeforeItsFirstPictureTheQuadIsInvisible()
    {
        var (wall, _, _) = Make();

        Assert.Equal(0, wall.Material.BaseColour.W);
        Assert.Null(wall.Material.BaseColourMap);
    }

    [Fact]
    public void ThePictureShowsTheWholeFaceTheRightWayUp()
    {
        var (wall, _, _) = Make();
        wall.Sync();

        var material = wall.Material;

        Assert.Equal(MaterialShading.Unlit, material.Shading);
        Assert.Equal(MaterialBlend.Fade, material.Blend);
        Assert.Equal(MaterialWrap.Clamp, material.Wrap);
        Assert.Equal(new Vector2(1, -1), material.Tiling);
        Assert.Equal(new Vector2(0, 1), material.Offset);
        Assert.Equal(1, material.BaseColour.W);
        Assert.Equal(Cells(Wall), Cells(wall.Quad.Transform));
    }
}
