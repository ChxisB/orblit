using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Examples.Tests;

/// <summary>
/// The gallery loading a scene file, with no editor in the process. An example
/// that claims to draw a scene file is only worth having if the file is really
/// being read, so this is checked rather than shown.
/// </summary>
public sealed class SceneFilesTests
{
    private static readonly RenderCamera Anywhere = new() { Position = new Vector3(0, 2, 10), Target = Vector3.Zero() };

    [Fact]
    public void TheSolidDocumentBecomesLitGeometryUnderWeather()
    {
        var example = new SceneFilesExample();

        var scene = example.Scene(Anywhere, 0);

        // A floor, two stacked crates and a loose one.
        Assert.Equal(4, scene.Objects.Count);
        Assert.Equal(RenderLightKind.Directional, Assert.Single(scene.Lights).Kind);
        Assert.True(scene.Fog.Density > 0);
        Assert.Null(example.Note);
    }

    /// <summary>
    /// The stack sits at (-3, 0.5), and the upper crate at (0, 1.05) within it.
    /// The sum, 1.55, appears nowhere in the file, so a crate there can only
    /// have come from the two being composed.
    /// </summary>
    [Fact]
    public void AStackedCrateIsPlacedByItsParentAsWellAsByItself()
    {
        var upper = new SceneFilesExample().Scene(Anywhere, 0).Objects
            .First(one => one.Transform.GetTranslation().Y > 1).Transform.GetTranslation();

        Assert.Equal(-3, upper.X, 0.001);
        Assert.Equal(1.55, upper.Y, 0.001);
    }

    [Fact]
    public void TheFlatDocumentBecomesSpriteLayersAndNoGeometry()
    {
        var example = new SceneFilesExample();
        Show(example, "2D scene");

        var scene = example.Scene(Anywhere, 0);

        Assert.Empty(scene.Objects);
        Assert.Equal(5, scene.Sprites.Count);
        // The spark is parented to the coin, two units left of centre.
        Assert.Contains(scene.Sprites, layer => Math.Abs(layer.Transform.GetTranslation().X + 2) < 0.001);
        Assert.Null(example.Note);
        Assert.Equal("Lift the coin", example.Settings()[1].Label);
    }

    [Fact]
    public void SwitchingBackRereadsTheOtherDocument()
    {
        var example = new SceneFilesExample();
        Show(example, "2D scene");
        Assert.Empty(example.Scene(Anywhere, 0).Objects);

        example.Control<Setting>("Lift the coin").Change(2);

        Show(example, "3D scene");

        Assert.Equal(4, example.Scene(Anywhere, 0).Objects.Count);
        // A fresh document has nothing lifted, whatever the last one had.
        Assert.Equal(0, example.Control<Setting>("Lift the crate").Value);
    }

    /// <summary>The slider runs from 0 to 5 and prints one decimal and a unit.</summary>
    [Fact]
    public void TheLiftReadsAsTheFixtureOneDid()
    {
        var lift = new SceneFilesExample().Control<Setting>("Lift the crate");

        Assert.Equal((0.0, 5.0, " m", 1), (lift.Min, lift.Max, lift.Unit, lift.Decimals));
    }

    [Fact]
    public void AnEditMovesOneEntityAsADiffRatherThanARebuild()
    {
        var example = new SceneFilesExample();
        var before = example.Scene(Anywhere, 0).Objects.Select(one => one.Key).ToList();
        Assert.Equal("Edits are applied as diffs.", example.Settings()[2].Label);

        example.Control<Setting>("Lift the crate").Change(3);

        var after = example.Scene(Anywhere, 0).Objects;
        // The renderer keeps what it already built.
        Assert.Equal(before, after.Select(one => one.Key));
        Assert.Contains(after, one => Math.Abs(one.Transform.GetTranslation().Y - 3) < 0.001);
        // Moving one thing along one axis is one operation.
        Assert.Equal("Last edit: 1 operation, and only the entity it named was rebuilt.", example.Settings()[2].Label);
        Assert.Equal(3, example.Control<Setting>("Lift the crate").Value);
    }

    /// <summary>The flat document is looked at square on, whatever the gallery's camera is doing.</summary>
    [Fact]
    public void TheFlatDocumentIsSeenSquareOn()
    {
        var example = new SceneFilesExample();
        Show(example, "2D scene");

        var camera = example.Scene(Anywhere, 0).Camera;

        Assert.True(camera.Orthographic);
        Assert.Equal(14, camera.ViewHeight);
        Assert.Equal(0, example.ViewPoint.Yaw);
    }

    /// <summary>
    /// Digests from a one-off run of the previous example. The fixture only draws
    /// the solid document at rest. The previous view handed a sprite's colour to the
    /// renderer unconverted, where the renderer reads linear light, so the 2D
    /// run had that one conversion added. Without it the previous run gives
    /// 6719f33f36393c0b and 9f7a3050f64e634f, and only the sprite colours differ.
    /// </summary>
    [Theory]
    [InlineData("2D scene", null, "7be59899b0d465be")]
    [InlineData("3D scene", 3.0, "48f82629872f03fd")]
    [InlineData("2D scene", 2.0, "d8dcd631bc72ea4e")]
    public void ADocumentMatchesTheFixture(string document, double? lift, string digest)
    {
        var example = new SceneFilesExample();
        Show(example, document);
        if (lift is { } metres)
        {
            example.Settings().OfType<Setting>().Single().Change(metres);
        }

        Assert.Equal(digest, SceneDigest.Of(SceneDigest.Message(example, 0)));
    }

    private static void Show(Example example, string option)
    {
        var select = example.Control<Choice>("").Select;
        Assert.NotNull(select);
        select(option);
    }
}
