using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

public class DebugLinesTests
{
    private static DebugLine Line(Vector3 from, Vector3 to) => new(from, to, new Vector3(1, 0, 0));

    // The cube is two metres a side, so its long axis runs from y = -1 to y = 1.
    private static Vector3 End(RenderObject mark, double y) => mark.Transform.Transformed3(new Vector3(0, y, 0));

    private static void ExpectAt(Vector3 actual, Vector3 expected) =>
        Assert.True((actual - expected).Length < 1e-9, actual.ToString());

    [Fact]
    public void AddsNothingWhileItIsOffAndNeverAsksForTheLines()
    {
        var asked = false;
        var scene = Scenes.One();

        var drawn = new DebugLines().Draw(scene, () =>
        {
            asked = true;
            return [Line(Vector3.Zero(), new Vector3(0, 1, 0))];
        });

        Assert.Same(scene, drawn);
        Assert.False(asked);
    }

    [Fact]
    public void AddsOneColouredCubeAfterTheSceneObjectsForEachLine()
    {
        var lines = new DebugLines(enabled: true);

        var drawn = lines.Draw(Scenes.One(), () =>
        [
            Line(Vector3.Zero(), new Vector3(0, 1, 0)),
            Line(Vector3.Zero(), new Vector3(1, 0, 0)),
        ]);

        Assert.Equal([1, lines.KeyBase, lines.KeyBase + 1], drawn.Objects.Select(o => o.Key));
        Assert.Equal(new Vector3(1, 0, 0), drawn.Objects[^1].Colour);
        Assert.False(drawn.Objects[^1].CastShadows);
        Assert.False(drawn.Objects[^1].ReceiveShadows);
    }

    [Fact]
    public void RunsEachCubeFromOneEndOfItsLineToTheOther()
    {
        var lines = new DebugLines(enabled: true);
        var from = new Vector3(1, 2, 3);

        foreach (var to in new[] { new Vector3(1, 5, 3), new Vector3(1, -1, 3), new Vector3(4, 2, 3), new Vector3(1, 2, -4) })
        {
            var mark = lines.Draw(Scenes.One(), () => [Line(from, to)]).Objects[^1];
            ExpectAt(End(mark, -1), from);
            ExpectAt(End(mark, 1), to);
        }
    }

    [Fact]
    public void IsAsThickAsAsked()
    {
        var lines = new DebugLines(enabled: true, width: 0.1);

        var mark = lines.Draw(Scenes.One(), () => [Line(Vector3.Zero(), new Vector3(0, 2, 0))]).Objects[^1];

        ExpectAt(mark.Transform.Transformed3(new Vector3(1, 0, 0)), new Vector3(0.05, 1, 0));
    }

    [Fact]
    public void DrawsAPointAsASquareOfTheWidth()
    {
        var lines = new DebugLines(enabled: true, width: 0.1);
        var at = new Vector3(2, 3, 4);

        var mark = lines.Draw(Scenes.One(), () => [Line(at, at)]).Objects[^1];

        ExpectAt(mark.Transform.Transformed3(Vector3.Zero()), at);
        ExpectAt(End(mark, 1) - End(mark, -1), new Vector3(0, 0.1, 0));
    }

    [Fact]
    public void SkipsALineThatHasNoPlace()
    {
        var lines = new DebugLines(enabled: true);

        var drawn = lines.Draw(Scenes.One(), () =>
        [
            Line(Vector3.Zero(), new Vector3(double.NaN, 0, 0)),
            Line(Vector3.Zero(), new Vector3(0, double.PositiveInfinity, 0)),
            Line(Vector3.Zero(), new Vector3(0, 1, 0)),
        ]);

        Assert.Equal([1, lines.KeyBase], drawn.Objects.Select(o => o.Key));
    }

    [Fact]
    public void GivesTheSceneBackWhenNoLineHasAPlace()
    {
        var scene = Scenes.One();

        var drawn = new DebugLines(enabled: true).Draw(scene, () => [Line(Vector3.Zero(), new Vector3(double.NaN, 0, 0))]);

        Assert.Same(scene, drawn);
    }

    [Fact]
    public void CanBeSwitchedOnAfterItIsMade()
    {
        var lines = new DebugLines { Enabled = true };

        var drawn = lines.Draw(Scenes.One(), () => [Line(Vector3.Zero(), new Vector3(0, 1, 0))]);

        Assert.Equal(2, drawn.Objects.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesAWidthThatIsNotAboveZero(double width) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DebugLines(width: width));
}
