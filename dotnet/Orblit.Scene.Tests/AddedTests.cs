using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

/// <summary>What the port checks that the Dart tests do not.</summary>
public class AddedTests
{
    [Fact]
    public void EveryJointKindSaysWhichWaysItReads()
    {
        var expected = new Dictionary<JointKind, JointAxis[]>
        {
            [JointKind.Fixed] = [],
            [JointKind.Point] = [],
            [JointKind.Hinge] = [JointAxis.AboutX],
            [JointKind.Cone] = [JointAxis.AboutX],
            [JointKind.Slider] = [JointAxis.AlongX],
            [JointKind.Distance] = [JointAxis.AlongX],
            [JointKind.Path] = [JointAxis.AlongX],
            [JointKind.Gear] = [],
            [JointKind.Rack] = [],
            [JointKind.Pulley] = [],
            [JointKind.SixAxis] = Enum.GetValues<JointAxis>(),
        };

        Assert.Equal(Enum.GetValues<JointKind>().Order(), expected.Keys.Order());
        foreach (var (kind, axes) in expected)
        {
            Assert.Equal(axes, new JointComponent(kind: kind).Axes);
        }
    }

    [Fact]
    public void ATurnIsMeasuredInDegreesAndAMoveInMetres()
    {
        foreach (var axis in Enum.GetValues<JointAxis>())
        {
            Assert.Equal(EnumNames.Of(axis).StartsWith("about", StringComparison.Ordinal), JointAxes.Turns(axis));
        }
    }

    [Fact]
    public void TheOrderNamesEveryReaderOnce()
    {
        Assert.Equal(SceneComponents.Order.Count, SceneComponents.Order.Distinct().Count());
        Assert.Equal(SceneComponents.Readers.Keys.Order(), SceneComponents.Order.Order());
    }

    [Fact]
    public void EveryReaderBuildsTheTypeItIsRegisteredUnder()
    {
        foreach (var type in SceneComponents.Order)
        {
            Assert.Equal(type, SceneComponents.Read(type, Map()).Type);
        }
    }

    [Fact]
    public void ReadingNothingWritesSomethingThatReadsBackTheSame()
    {
        foreach (var type in SceneComponents.Order)
        {
            var written = SceneComponents.Read(type, Map()).ToJson();

            Same(written, SceneComponents.Read(type, written).ToJson());
        }
    }

    [Fact]
    public void AComponentItDoesNotKnowIsKeptWholeAndWrittenBackAsItCame()
    {
        var json = Map(("shape", "round"), ("sides", List(1L, 2.5, "x")));

        var unknown = Assert.IsType<UnknownComponent>(SceneComponents.Read("widget", json));

        Assert.Equal("widget", unknown.Type);
        Same(json, unknown.ToJson());
    }

    [Fact]
    public void AMaskThatIsNotFiniteIsTheFallbackNotACrash()
    {
        Assert.Equal(7, Values.Bits(Map(("layers", double.PositiveInfinity)), "layers", 7));
        Assert.Equal(7, Values.Bits(Map(("layers", double.NaN)), "layers", 7));
    }
}
