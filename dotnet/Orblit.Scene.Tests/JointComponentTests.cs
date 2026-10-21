using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class JointComponentTests
{
    // a joint component

    [Fact]
    public void IsAnUnlimitedHingeWhenTheFileSaysNothing()
    {
        var joint = JointComponent.FromJson(Map());

        Assert.Equal(JointKind.Hinge, joint.Kind);
        Assert.Empty(joint.Limits);
        Assert.Equal(45, joint.Swing);
        Assert.Equal(0, joint.Strength);
        Assert.Equal(0, joint.BreakingForce);
        Assert.False(joint.Collide);
    }

    [Fact]
    public void IsWrittenAfterTheBodyAndBeforeTheMotion()
    {
        var order = SceneComponents.Order.ToList();

        Assert.Equal(order.IndexOf("body") + 1, order.IndexOf(SceneComponents.Joint));
        Assert.Equal(order.IndexOf(SceneComponents.Joint) + 1, order.IndexOf("motion"));
    }

    [Fact]
    public void KeepsTheLimitsItsKindDoesNotRead()
    {
        var hinge = new JointComponent(limits: new Dictionary<JointAxis, JointRange> { [JointAxis.AboutX] = new(0, 90) });
        var back = JointComponent.FromJson(hinge.CopyWith(kind: JointKind.Slider).ToJson()).CopyWith(kind: JointKind.Hinge);

        Same(hinge.ToJson(), back.ToJson());
        Assert.Equal([JointAxis.AlongX], back.CopyWith(kind: JointKind.Slider).Axes);
    }

    [Fact]
    public void SkipsALimitItCannotReadAndKeepsTheRest()
    {
        var joint = JointComponent.FromJson(Map(
            ("kind", "sixAxis"),
            ("limits", Map(
                ("alongY", List(0L, 1L)),
                ("aboutZ", List(5L)),
                ("aboutY", "wide"),
                ("sideways", List(0L, 1L))))));

        Assert.Equal(JointKind.SixAxis, joint.Kind);
        Assert.Equal([JointAxis.AlongY], joint.Limits.Keys);
    }

    [Fact]
    public void ChangesOneLimitAtATime()
    {
        var joint = new JointComponent(kind: JointKind.SixAxis)
            .Limit(JointAxis.AlongY, JointRange.At(0))
            .Limit(JointAxis.AboutZ, new JointRange(-5, 5));

        Assert.True(joint.Limits[JointAxis.AlongY].Locked);
        Assert.Equal([JointAxis.AboutZ], joint.Free(JointAxis.AlongY).Limits.Keys);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<JointAxis, JointRange>)joint.Limits)[JointAxis.AlongX] = new JointRange(0, 1));
    }

    // a joint that gives

    [Fact]
    public void HasNoGiveWhenTheFileSaysNothing()
    {
        var joint = JointComponent.FromJson(Map());

        Assert.Empty(joint.Springs);
        Assert.Equal(0, joint.SoftHertz);
        Assert.Equal(0, joint.GoalHertz);
        Assert.Equal(1, joint.Ratio);
        Assert.Empty(joint.Over);
        Assert.Empty(joint.Path);
        Assert.False(joint.Follow);
    }

    [Fact]
    public void GiveSurvivesTheRoundTrip()
    {
        var machine = new JointComponent(
            kind: JointKind.Path,
            limits: new Dictionary<JointAxis, JointRange> { [JointAxis.AlongX] = new(0, 4) },
            springs: new Dictionary<JointAxis, JointGive> { [JointAxis.AlongX] = new(2, 0.5, 1.5) },
            softHertz: 3,
            softRatio: 0.7,
            goal: 40,
            goalHertz: 6,
            goalRatio: 0.9,
            ratio: -0.5,
            axisB: new Vector3(0, 0, 1),
            over: [new Vector3(0, 3, 0), new Vector3(2, 3, 0)],
            path: [new Vector3(0, 0, 0), new Vector3(1, -1, 0), new Vector3(3, -1, 0)],
            follow: true);
        var back = JointComponent.FromJson(machine.ToJson());

        Same(machine.ToJson(), back.ToJson());
        Assert.Equal(new JointGive(2, 0.5, 1.5), back.Springs[JointAxis.AlongX]);
        Assert.Equal(3, back.Path.Count);
        Assert.Equal(3, back.Path[2].X);
        Assert.Equal(3, back.Over[1].Y);
        Assert.Equal(1, back.AxisB.Z);
        Assert.True(back.Follow);
    }

    [Fact]
    public void ReadsTheNewKindsByName()
    {
        foreach (var kind in new[] { "gear", "rack", "pulley", "path" })
        {
            Assert.Equal(kind, EnumNames.Of(JointComponent.FromJson(Map(("kind", kind))).Kind));
        }
    }

    [Fact]
    public void SaysWhichWaysEachNewKindReads()
    {
        Assert.Equal([JointAxis.AlongX], new JointComponent(kind: JointKind.Path).Axes);
        foreach (var kind in new[] { JointKind.Gear, JointKind.Rack, JointKind.Pulley })
        {
            Assert.Empty(new JointComponent(kind: kind).Axes);
        }
    }

    [Fact]
    public void SkipsASpringWithNoFrequencyAndKeepsTheRest()
    {
        var joint = JointComponent.FromJson(Map(("springs", Map(
            ("alongX", List(0L, 1L, 0L)),
            ("aboutX", List(4L)),
            ("aboutY", "stiff")))));

        Assert.Equal([JointAxis.AboutX], joint.Springs.Keys);
        Assert.Equal(1, joint.Springs[JointAxis.AboutX].Ratio);
        Assert.Null(joint.Springs[JointAxis.AboutX].Rest);
    }

    [Fact]
    public void DropsAHalfWrittenPoint()
    {
        var joint = JointComponent.FromJson(Map(("path", List(0L, 0L, 0L, 1L, 2L))));

        Assert.Single(joint.Path);
    }

    [Fact]
    public void CopiesAndChangesOneSpringAtATime()
    {
        var joint = new JointComponent()
            .Spring(JointAxis.AboutX, new JointGive(5))
            .Spring(JointAxis.AlongY, new JointGive(1, Rest: 2));

        Assert.Equal([JointAxis.AboutX, JointAxis.AlongY], joint.Springs.Keys);
        Assert.Equal([JointAxis.AlongY], joint.Unspring(JointAxis.AboutX).Springs.Keys);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<JointAxis, JointGive>)joint.Springs)[JointAxis.AlongZ] = new JointGive(1));
    }

    [Fact]
    public void DoesNotShareItsPointsWithTheVectorItWasGiven()
    {
        var point = new Vector3(1, 2, 3);
        var joint = new JointComponent(path: [point]);
        point.X = 9;

        Assert.Equal(1, joint.Path.Single().X);
    }

    // what a joint holds

    // world
    //   door        body
    //   arm         body
    //     elbow     joint
    //     forearm   body
    //       wrist   joint
    //       hand    (no body)
    //         grip  joint
    //   loose       joint, nothing above it with a body
    private static readonly Dictionary<string, string?> Parents = new()
    {
        ["door"] = null,
        ["arm"] = null,
        ["elbow"] = "arm",
        ["forearm"] = "arm",
        ["wrist"] = "forearm",
        ["hand"] = "forearm",
        ["grip"] = "hand",
        ["loose"] = null,
    };

    private static readonly HashSet<string> Bodies = ["door", "arm", "forearm"];

    private static (string? Body, string? Holder) Ends(string? body, string? holder) => (body, holder);

    private static (string? Body, string? Holder) EndsOf(string id) =>
        JointComponent.EndsOf(id, at => Parents[at], Bodies.Contains);

    [Fact]
    public void IsItsOwnBodyHeldByTheWorldWhenNothingIsAboveIt() =>
        Assert.Equal(Ends("door", null), EndsOf("door"));

    [Fact]
    public void IsTheBodyItHangsUnderHeldByTheOneAboveThat()
    {
        Assert.Equal(Ends("forearm", "arm"), EndsOf("wrist"));
        Assert.Equal(Ends("forearm", "arm"), EndsOf("grip"));
    }

    [Fact]
    public void OnABodyHoldsThatBodyToTheOneAboveIt() =>
        Assert.Equal(Ends("forearm", "arm"), EndsOf("forearm"));

    [Fact]
    public void UnderABodyWithNothingAboveHoldsItToTheWorld() =>
        Assert.Equal(Ends("arm", null), EndsOf("elbow"));

    [Fact]
    public void HoldsNothingWithNoBodyAtOrAboveIt() =>
        Assert.Equal(Ends(null, null), EndsOf("loose"));

    [Fact]
    public void SurvivesAParentLoop()
    {
        var ends = JointComponent.EndsOf("a", at => at == "a" ? "b" : "a", at => at == "b");

        Assert.Equal(Ends("b", null), ends);
    }
}
